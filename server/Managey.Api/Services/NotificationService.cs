using Managey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Services;

/// <summary>Emails whoever needs to act next on a request, or the requester once it is decided.</summary>
public class NotificationService(AppDbContext db, EmailQueue queue, IHttpContextAccessor http)
{
    /// <summary>Call after saving a change to a request.</summary>
    public async Task RequestChangedAsync(int requestId)
    {
        var request = await db.Requests
            .Include(r => r.Requester)
            .Include(r => r.Steps).ThenInclude(s => s.ApproverUser)
            .Include(r => r.Steps).ThenInclude(s => s.ActedBy)
            .AsSplitQuery()
            .FirstAsync(r => r.Id == requestId);

        var link = $"{BaseUrl()}/requests/{request.Id}";
        var summary = Summary(request);

        if (request.Status == RequestStatus.Pending)
        {
            var step = request.Steps.First(s => s.Status == StepStatus.Pending);
            foreach (var approver in await ApproversAsync(step, request.RequesterId))
            {
                Send(approver, $"Approval needed: {request.RefNo} {request.Title}",
                    $"Hi {approver.FullName},\n\n{request.Requester!.FullName} has a request waiting for your approval.\n\n{summary}\n\nReview it here: {link}\n");
            }
        }
        else if (request.Status is RequestStatus.Approved or RequestStatus.Rejected)
        {
            var decided = request.Steps.Where(s => s.ActedAt != null).OrderBy(s => s.ActedAt).LastOrDefault();
            var verdict = request.Status == RequestStatus.Approved ? "approved" : "rejected";
            var by = decided?.ActedBy is { } u ? $" by {u.FullName}" : "";
            var comment = string.IsNullOrEmpty(decided?.Comment) ? "" : $"\nComment: {decided.Comment}\n";
            Send(request.Requester!, $"Request {verdict}: {request.RefNo} {request.Title}",
                $"Hi {request.Requester!.FullName},\n\nYour request has been {verdict}{by}.\n{comment}\n{summary}\n\nDetails: {link}\n");
        }
    }

    private async Task<List<User>> ApproversAsync(ApprovalStep step, int requesterId)
    {
        if (step.ApproverUser is { } person) return [person];

        var role = step.ApproverKind == ApproverKind.HR ? UserRole.HR : UserRole.Admin;
        var users = await db.Users.Where(u => u.IsActive && u.Role == role && u.Id != requesterId && u.Email != null).ToListAsync();
        // Admins can act on any step, so they cover for a company without HR staff.
        if (users.Count == 0 && role == UserRole.HR)
            users = await db.Users.Where(u => u.IsActive && u.Role == UserRole.Admin && u.Id != requesterId && u.Email != null).ToListAsync();
        return users;
    }

    private void Send(User to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(to.Email)) return;
        queue.Enqueue(new EmailMessage(to.Email, to.FullName, subject, body + "\n— Managey"));
    }

    private static string Summary(Request r)
    {
        var lines = new List<string> { $"Reference: {r.RefNo}", $"Type: {r.Type}", $"Title: {r.Title}" };
        if (r.Type == RequestType.Leave)
            lines.Add($"Dates: {r.StartDate:dd MMM yyyy} – {r.EndDate:dd MMM yyyy} ({r.Days} day(s))");
        if (r.Amount is { } amount) lines.Add($"Amount: RM {amount:N2}");
        return string.Join('\n', lines);
    }

    private string BaseUrl()
    {
        var req = http.HttpContext?.Request;
        return req is null ? "" : $"{req.Scheme}://{req.Host}";
    }
}
