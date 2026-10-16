using System.Security.Claims;
using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController(
    AppDbContext db, AuditService audit, WorkflowService workflow, LeaveService leave, FileStorage files,
    NotificationService notify) : ControllerBase
{
    public class RequestInput
    {
        public RequestType Type { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int? LeaveTypeId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool HalfDay { get; set; }
        public decimal? Amount { get; set; }
        public List<IFormFile> Files { get; set; } = new();
    }

    public record ActionInput(string? Comment);

    /// <summary>scope: mine (default), approvals (decided or pending for me), all (HR/admin).</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string scope = "mine", [FromQuery] RequestStatus? status = null, [FromQuery] RequestType? type = null)
    {
        var me = User.UserId();
        var query = db.Requests.AsQueryable();
        switch (scope)
        {
            case "all":
                if (!User.IsAdminOrHR()) return Forbid();
                break;
            case "pending":
                var pendingIds = workflow.PendingFor(User).Select(s => s.RequestId);
                query = query.Where(r => pendingIds.Contains(r.Id));
                break;
            case "history":
                query = query.Where(r => r.Steps.Any(s => s.ActedById == me));
                break;
            default:
                query = query.Where(r => r.RequesterId == me);
                break;
        }
        if (status is not null) query = query.Where(r => r.Status == status);
        if (type is not null) query = query.Where(r => r.Type == type);

        var items = await query
            .OrderByDescending(r => r.Id)
            .Take(500)
            .Select(r => new
            {
                r.Id, r.RefNo, r.Type, r.Title, r.Status, r.CreatedAt, r.CompletedAt,
                Requester = r.Requester!.FullName,
                LeaveType = r.LeaveType!.Name, r.StartDate, r.EndDate, r.Days, r.Amount,
                CurrentStep = r.Steps.Where(s => s.Status == StepStatus.Pending)
                    .Select(s => s.ApproverUser != null ? s.ApproverUser.FullName : s.ApproverKind.ToString())
                    .FirstOrDefault(),
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var request = await LoadAsync(id);
        if (request is null) return NotFound();
        if (!CanView(request)) return Forbid();

        var me = User.UserId();
        var current = request.Steps.FirstOrDefault(s => s.Status == StepStatus.Pending);
        return Ok(new
        {
            request.Id, request.RefNo, request.Type, request.Title, request.Description, request.Status,
            request.CreatedAt, request.CompletedAt,
            Requester = new { request.Requester!.Id, request.Requester.FullName, request.Requester.EmployeeNo },
            request.LeaveTypeId, LeaveType = request.LeaveType?.Name,
            request.StartDate, request.EndDate, request.HalfDay, request.Days, request.Amount,
            Steps = request.Steps.OrderBy(s => s.StepOrder).Select(s => new
            {
                s.Id, s.StepOrder, s.ApproverKind, s.Status, s.ActedAt, s.Comment,
                Approver = s.ApproverUser?.FullName,
                ActedBy = s.ActedBy?.FullName,
            }),
            Attachments = request.Attachments.OrderBy(a => a.Id).Select(a => new { a.Id, a.FileName, a.Size, a.UploadedAt }),
            CanAct = current is not null && WorkflowService.CanAct(current, request, User),
            CanCancel = request.RequesterId == me && request.Status == RequestStatus.Pending,
        });
    }

    [HttpPost]
    [RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] RequestInput input)
    {
        var me = await db.Users.FirstAsync(u => u.Id == User.UserId());
        var request = new Request
        {
            Type = input.Type,
            Title = input.Title?.Trim() ?? "",
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            RequesterId = me.Id,
        };

        if (input.Files.Count > 5) return BadRequest(new { message = "Attach at most 5 files." });
        foreach (var file in input.Files)
        {
            if (FileStorage.Validate(file) is { } fileError) return BadRequest(new { message = $"{file.FileName}: {fileError}" });
        }

        switch (input.Type)
        {
            case RequestType.Leave:
                if (await ValidateLeaveAsync(request, input, me.Id) is { } leaveError) return BadRequest(new { message = leaveError });
                break;
            case RequestType.Claim or RequestType.Purchase:
                if (input.Amount is not > 0) return BadRequest(new { message = "Amount must be greater than zero." });
                request.Amount = Math.Round(input.Amount.Value, 2);
                break;
        }
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(new { message = "Title is required." });

        request.RefNo = await NextRefNoAsync(request.Type);
        await workflow.BuildStepsAsync(request, me);

        foreach (var file in input.Files)
        {
            request.Attachments.Add(new Document
            {
                FileName = Path.GetFileName(file.FileName),
                StoredName = await files.SaveAsync(file),
                ContentType = file.ContentType,
                Size = file.Length,
                UploadedById = me.Id,
            });
        }

        db.Requests.Add(request);
        await db.SaveChangesAsync();
        audit.Log("Create", nameof(Request), request.Id, $"{request.RefNo} {request.Title}");
        await db.SaveChangesAsync();
        await notify.RequestChangedAsync(request.Id);
        return Ok(new { request.Id, request.RefNo });
    }

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id, ActionInput input) => ActAsync(id, true, input.Comment);

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, ActionInput input) => ActAsync(id, false, input.Comment);

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var request = await LoadAsync(id);
        if (request is null) return NotFound();
        if (request.RequesterId != User.UserId()) return Forbid();
        if (request.Status != RequestStatus.Pending) return BadRequest(new { message = "Only pending requests can be cancelled." });

        WorkflowService.Cancel(request);
        audit.Log("Cancel", nameof(Request), id, request.RefNo);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult> ActAsync(int id, bool approve, string? comment)
    {
        var request = await LoadAsync(id);
        if (request is null) return NotFound();
        var step = request.Steps.FirstOrDefault(s => s.Status == StepStatus.Pending);
        if (step is null || !WorkflowService.CanAct(step, request, User)) return Forbid();
        if (!approve && string.IsNullOrWhiteSpace(comment))
            return BadRequest(new { message = "Please give a reason for rejecting." });

        WorkflowService.Act(request, step, approve, comment, User.UserId());
        audit.Log(approve ? "Approve" : "Reject", nameof(Request), id, $"{request.RefNo} step {step.StepOrder}");
        await db.SaveChangesAsync();
        await notify.RequestChangedAsync(id);
        return NoContent();
    }

    private async Task<string?> ValidateLeaveAsync(Request request, RequestInput input, int userId)
    {
        if (input.LeaveTypeId is null || input.StartDate is null || input.EndDate is null)
            return "Leave type, start date and end date are required.";
        var type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == input.LeaveTypeId && t.IsActive);
        if (type is null) return "Leave type not found.";

        var start = input.StartDate.Value.Date;
        var end = input.EndDate.Value.Date;
        if (end < start) return "End date cannot be before start date.";
        if (start.Year != end.Year) return "Split leave that crosses into a new year into two requests.";
        if (input.HalfDay && start != end) return "Half day leave must be a single day.";
        if (type.RequiresAttachment && input.Files.Count == 0) return $"{type.Name} leave requires a supporting document.";

        var days = await leave.CountWorkingDaysAsync(start, end, input.HalfDay);
        if (days == 0) return "The selected dates fall on weekends or public holidays.";

        var overlaps = await db.Requests.AnyAsync(r =>
            r.RequesterId == userId && r.Type == RequestType.Leave
            && (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Approved)
            && r.StartDate <= end && r.EndDate >= start);
        if (overlaps) return "You already have leave on some of these dates.";

        var balance = (await leave.GetBalancesAsync(userId, start.Year)).First(b => b.LeaveTypeId == type.Id);
        if (days > balance.Remaining) return $"Not enough {type.Name} leave. Remaining: {balance.Remaining} day(s).";

        request.LeaveTypeId = type.Id;
        request.StartDate = start;
        request.EndDate = end;
        request.HalfDay = input.HalfDay;
        request.Days = days;
        if (string.IsNullOrWhiteSpace(request.Title)) request.Title = $"{type.Name} leave";
        return null;
    }

    private async Task<string> NextRefNoAsync(RequestType type)
    {
        var prefix = type switch
        {
            RequestType.Leave => "LV",
            RequestType.Claim => "CL",
            RequestType.Purchase => "PR",
            _ => "GN",
        } + $"-{DateTime.Today.Year}-";
        var last = await db.Requests.Where(r => r.RefNo.StartsWith(prefix)).OrderByDescending(r => r.RefNo).Select(r => r.RefNo).FirstOrDefaultAsync();
        var next = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{next:D4}";
    }

    private Task<Request?> LoadAsync(int id) => db.Requests
        .Include(r => r.Requester)
        .Include(r => r.LeaveType)
        .Include(r => r.Steps).ThenInclude(s => s.ApproverUser)
        .Include(r => r.Steps).ThenInclude(s => s.ActedBy)
        .Include(r => r.Attachments)
        .AsSplitQuery()
        .FirstOrDefaultAsync(r => r.Id == id);

    internal static bool CanView(Request request, ClaimsPrincipal user)
    {
        var me = user.UserId();
        return request.RequesterId == me
               || user.IsAdminOrHR()
               || request.Steps.Any(s => s.ApproverUserId == me || s.ActedById == me);
    }

    private bool CanView(Request request) => CanView(request, User);
}
