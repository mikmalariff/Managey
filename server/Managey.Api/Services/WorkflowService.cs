using System.Security.Claims;
using Managey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Services;

public class WorkflowService(AppDbContext db)
{
    /// <summary>
    /// Snapshots the configured approval chain onto a new request. Steps that cannot apply
    /// (no manager, or the requester would approve themselves) are skipped. A request with
    /// no applicable steps is approved immediately.
    /// </summary>
    public async Task BuildStepsAsync(Request request, User requester)
    {
        var config = await db.WorkflowSteps
            .Where(w => w.RequestType == request.Type)
            .OrderBy(w => w.StepOrder)
            .ToListAsync();

        var order = 1;
        foreach (var w in config)
        {
            int? approverId = w.ApproverKind switch
            {
                ApproverKind.DirectManager => requester.ManagerId,
                ApproverKind.SpecificUser => w.ApproverUserId,
                _ => null,
            };
            var needsPerson = w.ApproverKind is ApproverKind.DirectManager or ApproverKind.SpecificUser;
            var skip = needsPerson && (approverId is null || approverId == requester.Id);

            request.Steps.Add(new ApprovalStep
            {
                StepOrder = order++,
                ApproverKind = w.ApproverKind,
                ApproverUserId = approverId,
                Status = skip ? StepStatus.Skipped : StepStatus.Waiting,
            });
        }

        AdvanceOrComplete(request);
    }

    public static bool CanAct(ApprovalStep step, Request request, ClaimsPrincipal user)
    {
        if (step.Status != StepStatus.Pending || request.Status != RequestStatus.Pending) return false;
        var userId = user.UserId();
        if (request.RequesterId == userId) return false;
        if (user.IsInRole(Roles.Admin)) return true;
        return step.ApproverKind switch
        {
            ApproverKind.HR => user.IsInRole(Roles.HR),
            ApproverKind.Admin => false,
            _ => step.ApproverUserId == userId,
        };
    }

    /// <summary>Pending steps the given user is allowed to act on.</summary>
    public IQueryable<ApprovalStep> PendingFor(ClaimsPrincipal user)
    {
        var userId = user.UserId();
        var isAdmin = user.IsInRole(Roles.Admin);
        var isHR = user.IsInRole(Roles.HR);
        return db.ApprovalSteps.Where(s =>
            s.Status == StepStatus.Pending
            && s.Request!.Status == RequestStatus.Pending
            && s.Request.RequesterId != userId
            && (isAdmin
                || s.ApproverUserId == userId
                || (s.ApproverKind == ApproverKind.HR && isHR)));
    }

    public static void Act(Request request, ApprovalStep step, bool approve, string? comment, int userId)
    {
        step.Status = approve ? StepStatus.Approved : StepStatus.Rejected;
        step.ActedById = userId;
        step.ActedAt = DateTime.UtcNow;
        step.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        if (approve)
        {
            AdvanceOrComplete(request);
            return;
        }

        request.Status = RequestStatus.Rejected;
        request.CompletedAt = DateTime.UtcNow;
        foreach (var s in request.Steps.Where(s => s.Status == StepStatus.Waiting)) s.Status = StepStatus.Skipped;
    }

    public static void Cancel(Request request)
    {
        request.Status = RequestStatus.Cancelled;
        request.CompletedAt = DateTime.UtcNow;
        foreach (var s in request.Steps.Where(s => s.Status is StepStatus.Waiting or StepStatus.Pending)) s.Status = StepStatus.Skipped;
    }

    private static void AdvanceOrComplete(Request request)
    {
        var next = request.Steps.OrderBy(s => s.StepOrder).FirstOrDefault(s => s.Status == StepStatus.Waiting);
        if (next is not null)
        {
            next.Status = StepStatus.Pending;
            return;
        }
        request.Status = RequestStatus.Approved;
        request.CompletedAt = DateTime.UtcNow;
    }
}
