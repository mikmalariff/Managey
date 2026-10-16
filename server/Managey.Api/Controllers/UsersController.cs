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
    public record PayItemInput(PayItemKind Kind, string Name, decimal Amount, bool Statutory);
    public record MembershipInput(int SchemeId, string? MemberNo, decimal? EmployeeAmount, decimal? EmployerAmount);
    public record PayInput(
        decimal? BasicSalary, string? IcNumber, string? PassportNo, string? TaxNo, string? BankName, string? BankAccountNo,
        List<PayItemInput> Items, List<MembershipInput> Contributions);
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
        await EnrolInDefaultSchemesAsync(db, [user.Id]);

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

    [HttpGet("{id:int}/pay")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> GetPay(int id)
    {
        var user = await db.Users.Include(u => u.Contributions).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        var items = await db.EmployeePayItems.Where(i => i.UserId == id).OrderBy(i => i.Kind).ThenBy(i => i.Id)
            .Select(i => new { i.Kind, i.Name, i.Amount, i.Statutory }).ToListAsync();
        return Ok(new
        {
            user.BasicSalary, user.IcNumber, user.PassportNo, user.TaxNo, user.BankName, user.BankAccountNo, Items = items,
            Contributions = user.Contributions.Select(c => new { c.SchemeId, c.MemberNo, c.EmployeeAmount, c.EmployerAmount }),
        });
    }

    [HttpPut("{id:int}/pay")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> UpdatePay(int id, PayInput input)
    {
        var user = await db.Users.Include(u => u.Contributions).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (input.BasicSalary < 0 || input.Items.Any(i => i.Amount < 0 || string.IsNullOrWhiteSpace(i.Name))
            || input.Contributions.Any(c => c.EmployeeAmount < 0 || c.EmployerAmount < 0))
            return BadRequest(new { message = "Amounts cannot be negative, and every item needs a name." });
        var schemeIds = await db.ContributionSchemes.Select(s => s.Id).ToListAsync();
        if (input.Contributions.Any(c => !schemeIds.Contains(c.SchemeId))) return BadRequest(new { message = "Unknown contribution scheme." });

        ApplyPay(user, input.BasicSalary, input.IcNumber, input.PassportNo, input.TaxNo, input.BankName, input.BankAccountNo);
        db.EmployeePayItems.RemoveRange(db.EmployeePayItems.Where(i => i.UserId == id));
        db.EmployeePayItems.AddRange(input.Items.Select(i => new EmployeePayItem
        {
            UserId = id, Kind = i.Kind, Name = i.Name.Trim(), Amount = Math.Round(i.Amount, 2), Statutory = i.Statutory,
        }));
        db.EmployeeContributions.RemoveRange(user.Contributions);
        user.Contributions = input.Contributions.DistinctBy(c => c.SchemeId).Select(c => new EmployeeContribution
        {
            UserId = id, SchemeId = c.SchemeId,
            MemberNo = string.IsNullOrWhiteSpace(c.MemberNo) ? null : c.MemberNo.Trim(),
            EmployeeAmount = c.EmployeeAmount is null ? null : Math.Round(c.EmployeeAmount.Value, 2),
            EmployerAmount = c.EmployerAmount is null ? null : Math.Round(c.EmployerAmount.Value, 2),
        }).ToList();
        audit.Log("UpdatePay", nameof(User), id, user.Username);
        await db.SaveChangesAsync();
        return NoContent();
    }

    internal static void ApplyPay(User user, decimal? basic, string? ic, string? passport, string? tax, string? bank, string? account)
    {
        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        user.BasicSalary = basic is null ? null : Math.Round(basic.Value, 2);
        user.IcNumber = Clean(ic);
        user.PassportNo = Clean(passport);
        user.TaxNo = Clean(tax);
        user.BankName = Clean(bank);
        user.BankAccountNo = Clean(account);
    }

    /// <summary>Adds new employees to every active scheme marked as default for new employees.</summary>
    internal static async Task EnrolInDefaultSchemesAsync(AppDbContext db, IReadOnlyCollection<int> userIds)
    {
        var schemes = await db.ContributionSchemes.Where(s => s.IsActive && s.DefaultForNewEmployees).Select(s => s.Id).ToListAsync();
        var existing = await db.EmployeeContributions.Where(c => userIds.Contains(c.UserId)).Select(c => new { c.UserId, c.SchemeId }).ToListAsync();
        foreach (var userId in userIds)
            foreach (var schemeId in schemes.Where(s => !existing.Any(e => e.UserId == userId && e.SchemeId == s)))
                db.EmployeeContributions.Add(new EmployeeContribution { UserId = userId, SchemeId = schemeId });
        await db.SaveChangesAsync();
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
