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
    public record LeaveTypeInput(string Name, decimal DefaultDays, bool RequiresAttachment, bool IsActive);

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
