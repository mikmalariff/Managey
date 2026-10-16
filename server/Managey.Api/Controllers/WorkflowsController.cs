using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

/// <summary>Approval chain per request type. Changes apply to new requests only.</summary>
[ApiController]
[Route("api/workflows")]
[Authorize(Roles = Roles.Admin)]
public class WorkflowsController(AppDbContext db, AuditService audit) : ControllerBase
{
    public record StepInput(ApproverKind ApproverKind, int? ApproverUserId);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var steps = await db.WorkflowSteps.OrderBy(w => w.StepOrder).ToListAsync();
        return Ok(Enum.GetValues<RequestType>().Select(type => new
        {
            Type = type,
            Steps = steps.Where(s => s.RequestType == type).Select(s => new { s.ApproverKind, s.ApproverUserId }),
        }));
    }

    [HttpPut("{type}")]
    public async Task<IActionResult> Update(RequestType type, List<StepInput> steps)
    {
        if (steps.Count > 5) return BadRequest(new { message = "A workflow can have at most 5 steps." });
        foreach (var step in steps)
        {
            if (step.ApproverKind == ApproverKind.SpecificUser &&
                (step.ApproverUserId is null || !await db.Users.AnyAsync(u => u.Id == step.ApproverUserId && u.IsActive)))
                return BadRequest(new { message = "Choose an active employee for each 'specific person' step." });
        }

        db.WorkflowSteps.RemoveRange(db.WorkflowSteps.Where(w => w.RequestType == type));
        db.WorkflowSteps.AddRange(steps.Select((s, i) => new WorkflowStep
        {
            RequestType = type,
            StepOrder = i + 1,
            ApproverKind = s.ApproverKind,
            ApproverUserId = s.ApproverKind == ApproverKind.SpecificUser ? s.ApproverUserId : null,
        }));
        audit.Log("Update", nameof(WorkflowStep), type, string.Join(" > ", steps.Select(s => s.ApproverKind)));
        await db.SaveChangesAsync();
        return NoContent();
    }
}
