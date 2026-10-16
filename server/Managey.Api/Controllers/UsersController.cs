using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(AppDbContext db, IPasswordHasher<User> hasher, AuditService audit, LeaveService leave) : ControllerBase
{
    public record UserInput(
        string EmployeeNo, string Username, string FullName, string? Email, string? Phone,
        UserRole Role, int? DepartmentId, string? Position, int? ManagerId, DateTime? JoinDate,
        bool IsActive, string? Password);

    public record PasswordInput(string Password);
    public record EntitlementInput(int LeaveTypeId, decimal? Days);

    public static string? PasswordProblem(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < 8 ? "Password must be at least 8 characters." : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false)
    {
        var query = db.Users.AsQueryable();
        if (!includeInactive || !User.IsAdminOrHR()) query = query.Where(u => u.IsActive);
        var users = await query
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id, u.EmployeeNo, u.Username, u.FullName, u.Email, u.Phone, u.Role,
                u.DepartmentId, Department = u.Department!.Name, u.Position,
                u.ManagerId, Manager = u.Manager!.FullName, u.JoinDate, u.IsActive, u.LastLoginAt,
            })
            .ToListAsync();
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await db.Users
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id, u.EmployeeNo, u.Username, u.FullName, u.Email, u.Phone, u.Role,
                u.DepartmentId, Department = u.Department!.Name, u.Position,
                u.ManagerId, Manager = u.Manager!.FullName, u.JoinDate, u.IsActive, u.LastLoginAt,
            })
            .FirstOrDefaultAsync();
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Create(UserInput input)
    {
        if (await ValidateAsync(input, null) is { } error) return BadRequest(new { message = error });
        if (PasswordProblem(input.Password) is { } problem) return BadRequest(new { message = problem });

        var user = new User();
        Apply(user, input);
        user.PasswordHash = hasher.HashPassword(user, input.Password!);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        audit.Log("Create", nameof(User), user.Id, $"{user.Username} ({user.Role})");
        await db.SaveChangesAsync();
        return Ok(new { user.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> Update(int id, UserInput input)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Role == UserRole.Admin && !User.IsInRole(Roles.Admin))
            return BadRequest(new { message = "Only an admin can edit an admin account." });
        if (await ValidateAsync(input, id) is { } error) return BadRequest(new { message = error });
        if (id == User.UserId() && (!input.IsActive || input.Role != user.Role))
            return BadRequest(new { message = "You cannot deactivate or change the role of your own account." });

        Apply(user, input);
        if (!string.IsNullOrEmpty(input.Password))
        {
            if (PasswordProblem(input.Password) is { } problem) return BadRequest(new { message = problem });
            user.PasswordHash = hasher.HashPassword(user, input.Password);
        }
        audit.Log("Update", nameof(User), user.Id, user.Username);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/leave")]
    public async Task<IActionResult> LeaveBalances(int id, [FromQuery] int? year)
    {
        if (!await CanViewHrDataAsync(id)) return Forbid();
        return Ok(await leave.GetBalancesAsync(id, year ?? DateTime.Today.Year));
    }

    [HttpPut("{id:int}/entitlements/{year:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> SetEntitlements(int id, int year, List<EntitlementInput> items)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id)) return NotFound();
        var existing = await db.LeaveEntitlements.Where(e => e.UserId == id && e.Year == year).ToListAsync();
        foreach (var item in items)
        {
            var row = existing.FirstOrDefault(e => e.LeaveTypeId == item.LeaveTypeId);
            if (item.Days is null)
            {
                if (row is not null) db.LeaveEntitlements.Remove(row);
            }
            else if (row is null)
            {
                db.LeaveEntitlements.Add(new LeaveEntitlement { UserId = id, LeaveTypeId = item.LeaveTypeId, Year = year, Days = item.Days.Value });
            }
            else
            {
                row.Days = item.Days.Value;
            }
        }
        audit.Log("UpdateEntitlements", nameof(User), id, year.ToString());
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>HR data is visible to the person themselves, HR/admin, and their direct manager.</summary>
    private async Task<bool> CanViewHrDataAsync(int userId)
    {
        var me = User.UserId();
        if (userId == me || User.IsAdminOrHR()) return true;
        return await db.Users.AnyAsync(u => u.Id == userId && u.ManagerId == me);
    }

    private async Task<string?> ValidateAsync(UserInput input, int? id)
    {
        if (string.IsNullOrWhiteSpace(input.Username) || string.IsNullOrWhiteSpace(input.FullName) || string.IsNullOrWhiteSpace(input.EmployeeNo))
            return "Employee no., username and full name are required.";
        if (input.Role == UserRole.Admin && !User.IsInRole(Roles.Admin))
            return "Only an admin can grant the admin role.";
        if (await db.Users.AnyAsync(u => u.Id != id && u.Username == input.Username.Trim()))
            return "Username is already taken.";
        if (await db.Users.AnyAsync(u => u.Id != id && u.EmployeeNo == input.EmployeeNo.Trim()))
            return "Employee no. is already in use.";
        if (input.ManagerId is { } managerId)
        {
            if (managerId == id) return "An employee cannot be their own manager.";
            if (!await db.Users.AnyAsync(u => u.Id == managerId)) return "Manager not found.";
        }
        return null;
    }

    private static void Apply(User user, UserInput input)
    {
        user.EmployeeNo = input.EmployeeNo.Trim();
        user.Username = input.Username.Trim();
        user.FullName = input.FullName.Trim();
        user.Email = input.Email?.Trim();
        user.Phone = input.Phone?.Trim();
        user.Role = input.Role;
        user.DepartmentId = input.DepartmentId;
        user.Position = input.Position?.Trim();
        user.ManagerId = input.ManagerId;
        user.JoinDate = input.JoinDate?.Date;
        user.IsActive = input.IsActive;
    }
}
