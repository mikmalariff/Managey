using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(AppDbContext db, WorkflowService workflow, LeaveService leave) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var me = User.UserId();
        var today = DateTime.Today;

        var onLeaveToday = await db.Requests
            .Where(r => r.Type == RequestType.Leave && r.Status == RequestStatus.Approved && r.StartDate <= today && r.EndDate >= today)
            .Select(r => new { r.Requester!.FullName, LeaveType = r.LeaveType!.Name, r.EndDate })
            .ToListAsync();

        var myAttendance = await db.Attendance
            .Where(a => a.UserId == me && a.Date == today)
            .Select(a => new { a.ClockIn, a.ClockOut })
            .FirstOrDefaultAsync();

        object? company = null;
        if (User.IsAdminOrHR())
        {
            var headcount = await db.Users.CountAsync(u => u.IsActive);
            var present = await db.Attendance.CountAsync(a => a.Date == today);
            var pendingRequests = await db.Requests.CountAsync(r => r.Status == RequestStatus.Pending);
            company = new { headcount, present, onLeave = onLeaveToday.Count, pendingRequests };
        }

        return Ok(new
        {
            Attendance = myAttendance,
            LeaveBalances = await leave.GetBalancesAsync(me, today.Year),
            MyPending = await db.Requests.CountAsync(r => r.RequesterId == me && r.Status == RequestStatus.Pending),
            ToApprove = await workflow.PendingFor(User).CountAsync(),
            RecentRequests = await db.Requests
                .Where(r => r.RequesterId == me)
                .OrderByDescending(r => r.Id).Take(5)
                .Select(r => new { r.Id, r.RefNo, r.Type, r.Title, r.Status, r.CreatedAt })
                .ToListAsync(),
            OnLeaveToday = onLeaveToday,
            Company = company,
        });
    }
}
