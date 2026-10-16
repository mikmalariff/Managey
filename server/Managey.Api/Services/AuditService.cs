using System.Security.Claims;
using Managey.Api.Data;

namespace Managey.Api.Services;

public class AuditService(AppDbContext db, IHttpContextAccessor http)
{
    /// <summary>Queues an audit entry; it is written on the caller's next SaveChanges.</summary>
    public void Log(string action, string entityType, object? entityId = null, string? details = null)
    {
        var principal = http.HttpContext?.User;
        int? userId = int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Username = principal?.Identity?.Name ?? "system",
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details,
        });
    }
}
