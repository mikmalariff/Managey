using System.Security.Claims;
using Managey.Api.Data;

namespace Managey.Api.Services;

public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string HR = nameof(UserRole.HR);
    public const string Manager = nameof(UserRole.Manager);
    public const string AdminOrHR = Admin + "," + HR;
}

public static class ClaimsPrincipalExtensions
{
    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Not signed in"));

    public static bool IsAdminOrHR(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.HR);
}
