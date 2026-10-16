using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController(AppDbContext db, AuditService audit) : ControllerBase
{
    public record DepartmentInput(string Name);

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.Departments
        .OrderBy(d => d.Name)
        .Select(d => new { d.Id, d.Name, Headcount = db.Users.Count(u => u.DepartmentId == d.Id && u.IsActive) })
        .ToListAsync());

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Create(DepartmentInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { message = "Name is required." });
        if (await db.Departments.AnyAsync(d => d.Name == input.Name.Trim())) return BadRequest(new { message = "Department already exists." });
        var dept = new Department { Name = input.Name.Trim() };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();
        audit.Log("Create", nameof(Department), dept.Id, dept.Name);
        await db.SaveChangesAsync();
        return Ok(new { dept.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Update(int id, DepartmentInput input)
    {
        var dept = await db.Departments.FindAsync(id);
        if (dept is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { message = "Name is required." });
        if (await db.Departments.AnyAsync(d => d.Id != id && d.Name == input.Name.Trim())) return BadRequest(new { message = "Department already exists." });
        dept.Name = input.Name.Trim();
        audit.Log("Update", nameof(Department), id, dept.Name);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Delete(int id)
    {
        var dept = await db.Departments.FindAsync(id);
        if (dept is null) return NotFound();
        if (await db.Users.AnyAsync(u => u.DepartmentId == id))
            return BadRequest(new { message = "Move employees out of this department before deleting it." });
        db.Departments.Remove(dept);
        audit.Log("Delete", nameof(Department), id, dept.Name);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/leave-types")]
[Authorize]
public class LeaveTypesController(AppDbContext db, AuditService audit) : ControllerBase
{
    public record LeaveTypeInput(string Name, decimal DefaultDays, bool RequiresAttachment, bool IsUnpaid, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await db.LeaveTypes.OrderBy(t => t.Name).ToListAsync());

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Create(LeaveTypeInput input)
    {
        if (Validate(input) is { } error) return BadRequest(new { message = error });
        if (await db.LeaveTypes.AnyAsync(t => t.Name == input.Name.Trim())) return BadRequest(new { message = "Leave type already exists." });
        var type = new LeaveType();
        Apply(type, input);
        db.LeaveTypes.Add(type);
        await db.SaveChangesAsync();
        audit.Log("Create", nameof(LeaveType), type.Id, type.Name);
        await db.SaveChangesAsync();
        return Ok(new { type.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Update(int id, LeaveTypeInput input)
    {
        var type = await db.LeaveTypes.FindAsync(id);
        if (type is null) return NotFound();
        if (Validate(input) is { } error) return BadRequest(new { message = error });
        if (await db.LeaveTypes.AnyAsync(t => t.Id != id && t.Name == input.Name.Trim())) return BadRequest(new { message = "Leave type already exists." });
        Apply(type, input);
        audit.Log("Update", nameof(LeaveType), id, type.Name);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(LeaveTypeInput input) =>
        string.IsNullOrWhiteSpace(input.Name) ? "Name is required."
        : input.DefaultDays < 0 ? "Days cannot be negative."
        : null;

    private static void Apply(LeaveType type, LeaveTypeInput input)
    {
        type.Name = input.Name.Trim();
        type.DefaultDays = input.DefaultDays;
        type.RequiresAttachment = input.RequiresAttachment;
        type.IsUnpaid = input.IsUnpaid;
        type.IsActive = input.IsActive;
    }
}

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(AppDbContext db, AuditService audit) : ControllerBase
{
    private static readonly string[] Editable = ["CompanyName", "WorkStartTime", "WorkEndTime"];

    [HttpGet]
    public async Task<IActionResult> Get() =>
        Ok(await db.Settings.Where(s => Editable.Contains(s.Key)).ToDictionaryAsync(s => s.Key, s => s.Value));

    [HttpPut]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(Dictionary<string, string?> values)
    {
        foreach (var (key, value) in values)
        {
            if (!Editable.Contains(key)) continue;
            if (key is "WorkStartTime" or "WorkEndTime" && !TimeOnly.TryParse(value, out _))
                return BadRequest(new { message = $"{key} must be a time such as 09:00." });
            var setting = await db.Settings.FindAsync(key);
            if (setting is null) db.Settings.Add(new Setting { Key = key, Value = value });
            else setting.Value = value;
        }
        audit.Log("Update", nameof(Setting), null, string.Join(", ", values.Keys));
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/audit")]
[Authorize(Roles = Roles.Admin)]
public class AuditController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] string? q = null)
    {
        const int pageSize = 50;
        var query = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.Username.Contains(q) || a.Action.Contains(q) || a.EntityType.Contains(q) || (a.Details != null && a.Details.Contains(q)));
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(a => a.Id).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }
}

[ApiController]
[Route("api/holidays")]
[Authorize]
public class HolidaysController(AppDbContext db, AuditService audit) : ControllerBase
{
    public record HolidayInput(DateTime Date, string Name);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? year)
    {
        var start = new DateTime(year ?? DateTime.Today.Year, 1, 1);
        return Ok(await db.Holidays
            .Where(h => h.Date >= start && h.Date < start.AddYears(1))
            .OrderBy(h => h.Date)
            .Select(h => new { h.Id, h.Date, h.Name })
            .ToListAsync());
    }

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Create(HolidayInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { message = "Name is required." });
        var date = input.Date.Date;
        if (await db.Holidays.AnyAsync(h => h.Date == date)) return BadRequest(new { message = "There is already a holiday on that date." });
        var holiday = new Holiday { Date = date, Name = input.Name.Trim() };
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync();
        audit.Log("Create", nameof(Holiday), holiday.Id, $"{date:yyyy-MM-dd} {holiday.Name}");
        await db.SaveChangesAsync();
        return Ok(new { holiday.Id });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Delete(int id)
    {
        var holiday = await db.Holidays.FindAsync(id);
        if (holiday is null) return NotFound();
        db.Holidays.Remove(holiday);
        audit.Log("Delete", nameof(Holiday), id, $"{holiday.Date:yyyy-MM-dd} {holiday.Name}");
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>SMTP settings for email notifications. Admin only; the password is never returned.</summary>
[ApiController]
[Route("api/settings/email")]
[Authorize(Roles = Roles.Admin)]
public class EmailSettingsController(AppDbContext db, AuditService audit, EmailSettings settings) : ControllerBase
{
    public record EmailInput(bool Enabled, string? Host, int Port, string Security, string? Username, string? Password, string? From);
    public record TestInput(string To);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var c = await settings.LoadAsync();
        var passwordSet = await db.Settings.AnyAsync(s => s.Key == EmailSettings.Prefix + "Password" && s.Value != null && s.Value != "");
        return Ok(new { c.Enabled, c.Host, c.Port, c.Security, c.Username, c.From, PasswordSet = passwordSet });
    }

    /// <summary>Password: null keeps the stored one, empty string clears it.</summary>
    [HttpPut]
    public async Task<IActionResult> Update(EmailInput input)
    {
        if (input.Enabled && (string.IsNullOrWhiteSpace(input.Host) || string.IsNullOrWhiteSpace(input.From)))
            return BadRequest(new { message = "Server and sender address are required to turn on email." });
        if (input.Port is < 1 or > 65535) return BadRequest(new { message = "Port must be between 1 and 65535." });
        if (input.Security is not ("Auto" or "None" or "StartTls" or "Ssl")) return BadRequest(new { message = "Unknown security option." });

        var values = new Dictionary<string, string?>
        {
            ["Enabled"] = input.Enabled ? "true" : "false",
            ["Host"] = input.Host?.Trim(),
            ["Port"] = input.Port.ToString(),
            ["Security"] = input.Security,
            ["Username"] = input.Username?.Trim(),
            ["From"] = input.From?.Trim(),
        };
        if (input.Password is not null)
            values["Password"] = input.Password == "" ? "" : settings.Protect(input.Password);

        foreach (var (name, value) in values)
        {
            var key = EmailSettings.Prefix + name;
            var row = await db.Settings.FindAsync(key);
            if (row is null) db.Settings.Add(new Setting { Key = key, Value = value });
            else row.Value = value;
        }
        audit.Log("Update", nameof(Setting), null, "Email settings");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test(TestInput input)
    {
        var config = await settings.LoadAsync();
        if (string.IsNullOrEmpty(config.Host) || string.IsNullOrEmpty(config.From))
            return BadRequest(new { message = "Save the server and sender address first." });
        try
        {
            await EmailSettings.SendAsync(config, new EmailMessage(input.To, input.To, "Managey test email",
                "This is a test email from Managey. If you can read this, email notifications are working.\n\n— Managey"));
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Could not send: {ex.Message}" });
        }
    }
}
