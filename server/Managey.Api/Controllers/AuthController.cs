using System.Security.Claims;
using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, AuditService audit) : ControllerBase
{
    public record LoginInput(string Username, string Password);
    public record ChangePasswordInput(string CurrentPassword, string NewPassword);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == input.Username.Trim());
        if (user is null || !user.IsActive ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        HttpContext.User = principal;

        user.LastLoginAt = DateTime.UtcNow;
        audit.Log("Login", nameof(User), user.Id);
        await db.SaveChangesAsync();
        return Ok(await MeAsync(user.Id));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var me = await MeAsync(User.UserId());
        return me is null ? Unauthorized() : Ok(me);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        var user = await db.Users.FindAsync(User.UserId());
        if (user is null) return Unauthorized();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect." });
        if (UsersController.PasswordProblem(input.NewPassword) is { } problem)
            return BadRequest(new { message = problem });

        user.PasswordHash = hasher.HashPassword(user, input.NewPassword);
        audit.Log("ChangePassword", nameof(User), user.Id);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<object?> MeAsync(int id)
    {
        var company = await db.Settings.Where(s => s.Key == "CompanyName").Select(s => s.Value).FirstOrDefaultAsync();
        return await db.Users
            .Where(u => u.Id == id && u.IsActive)
            .Select(u => new
            {
                u.Id, u.Username, u.FullName, u.EmployeeNo, u.Role,
                Department = u.Department!.Name,
                u.Position,
                IsManager = db.Users.Any(x => x.ManagerId == u.Id),
                CompanyName = company,
            })
            .FirstOrDefaultAsync();
    }
}
