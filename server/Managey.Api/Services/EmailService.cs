using System.Threading.Channels;
using Managey.Api.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace Managey.Api.Services;

public record EmailMessage(string To, string ToName, string Subject, string Body);

public record SmtpConfig(bool Enabled, string Host, int Port, string Security, string? Username, string? Password, string From, string FromName);

/// <summary>SMTP settings live in the Settings table; the password is encrypted with Data Protection.</summary>
public class EmailSettings(AppDbContext db, IDataProtectionProvider protection)
{
    public const string Prefix = "Email.";
    private readonly IDataProtector _protector = protection.CreateProtector("Managey.SmtpPassword");

    public async Task<SmtpConfig> LoadAsync()
    {
        var s = await db.Settings.Where(x => x.Key.StartsWith(Prefix)).ToDictionaryAsync(x => x.Key[Prefix.Length..], x => x.Value);
        var company = await db.Settings.Where(x => x.Key == "CompanyName").Select(x => x.Value).FirstOrDefaultAsync();
        string? password = null;
        if (!string.IsNullOrEmpty(s.GetValueOrDefault("Password")))
        {
            try { password = _protector.Unprotect(s["Password"]!); }
            catch { password = null; } // keys were lost; admin must re-enter the password
        }
        return new SmtpConfig(
            s.GetValueOrDefault("Enabled") == "true",
            s.GetValueOrDefault("Host") ?? "",
            int.TryParse(s.GetValueOrDefault("Port"), out var port) ? port : 587,
            s.GetValueOrDefault("Security") ?? "Auto",
            s.GetValueOrDefault("Username"),
            password,
            s.GetValueOrDefault("From") ?? "",
            company ?? "Managey");
    }

    public string Protect(string password) => _protector.Protect(password);

    public static async Task SendAsync(SmtpConfig config, EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(config.FromName, config.From));
        mime.To.Add(new MailboxAddress(message.ToName, message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        var security = config.Security switch
        {
            "None" => SecureSocketOptions.None,
            "StartTls" => SecureSocketOptions.StartTls,
            "Ssl" => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.Auto,
        };

        using var client = new SmtpClient { Timeout = 20000 };
        await client.ConnectAsync(config.Host, config.Port, security, ct);
        if (!string.IsNullOrEmpty(config.Username))
            await client.AuthenticateAsync(config.Username, config.Password ?? "", ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}

/// <summary>In-memory outbox so requests never wait on the mail server.</summary>
public class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public class EmailSender(EmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailSender> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var config = await scope.ServiceProvider.GetRequiredService<EmailSettings>().LoadAsync();
                if (!config.Enabled || string.IsNullOrEmpty(config.Host) || string.IsNullOrEmpty(config.From)) continue;
                await EmailSettings.SendAsync(config, message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not send email to {To}", message.To);
            }
        }
    }
}
