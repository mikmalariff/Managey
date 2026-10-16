using System.Text.Json.Serialization;
using Managey.Api.Controllers;
using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var isDevelopment = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Installed copies always use their own folder for data and wwwroot, wherever they are started from
    // (a Windows Service starts in System32). In development the project folder is used.
    ContentRootPath = isDevelopment ? null : AppContext.BaseDirectory,
});

// Runs as a Windows Service or a Linux systemd service; both are no-ops when started from a console.
builder.Host.UseWindowsService(options => options.ServiceName = "Managey");
builder.Host.UseSystemd();

var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration["Database:ConnectionString"] ?? "Data Source=data/managey.db";
if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<AppDbContext, SqlServerAppDbContext>(options => options.UseSqlServer(connectionString));
}
else
{
    var csb = new SqliteConnectionStringBuilder(connectionString);
    csb.DataSource = Path.GetFullPath(csb.DataSource, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(csb.DataSource)!);
    builder.Services.AddDbContext<AppDbContext, SqliteAppDbContext>(options => options.UseSqlite(csb.ToString()));
}

// Keys encrypt sign-in cookies and the stored SMTP password; keep them with the data so they survive restarts.
builder.Services.AddDataProtection()
    .SetApplicationName("Managey")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data", "keys")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<EmailSettings>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<PayrollSettings>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailSender>();
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "managey.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        // Over HTTPS the cookie is marked Secure; plain HTTP on a local network still works.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.SlidingExpiration = true;
        // The SPA handles navigation; API calls should get status codes, not redirects.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

// Slow down password guessing: 10 sign-in attempts per minute per address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// Behind a reverse proxy (IIS, nginx, Caddy, a cloud load balancer) the proxy terminates HTTPS and forwards the
// original scheme and client address. Only trust those headers when the app is configured to sit behind one.
var behindProxy = builder.Configuration.GetValue<bool>("ReverseProxy");
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, app.Configuration, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());

    // Recovery for a forgotten password, run on the server:  Managey.Api.exe --reset-password admin NewPassword123
    var reset = Array.IndexOf(args, "--reset-password");
    if (reset >= 0)
    {
        if (args.Length < reset + 3) { Console.Error.WriteLine("Usage: --reset-password <username> <new password>"); return 1; }
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == args[reset + 1]);
        if (user is null) { Console.Error.WriteLine($"No user named '{args[reset + 1]}'."); return 1; }
        if (UsersController.PasswordProblem(args[reset + 2]) is { } problem) { Console.Error.WriteLine(problem); return 1; }
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, args[reset + 2]);
        user.IsActive = true;
        db.AuditLogs.Add(new AuditLog { Username = "console", Action = "ResetPassword", EntityType = nameof(User), EntityId = user.Id.ToString() });
        await db.SaveChangesAsync();
        Console.WriteLine($"Password for '{user.Username}' has been reset.");
        return 0;
    }
}

if (behindProxy) app.UseForwardedHeaders();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
return 0;
