using Managey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Services;

public record LeaveBalance(int LeaveTypeId, string LeaveType, decimal Entitlement, decimal Used, decimal Pending, decimal Remaining);

public class LeaveService(AppDbContext db)
{
    /// <summary>Counts Monday–Friday days between two dates, inclusive.</summary>
    public static decimal CountWorkingDays(DateTime start, DateTime end, bool halfDay)
    {
        decimal days = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days++;
        }
        return halfDay && days == 1 ? 0.5m : days;
    }

    public async Task<List<LeaveBalance>> GetBalancesAsync(int userId, int year)
    {
        var types = await db.LeaveTypes.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
        var overrides = await db.LeaveEntitlements
            .Where(e => e.UserId == userId && e.Year == year)
            .ToDictionaryAsync(e => e.LeaveTypeId, e => e.Days);

        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = yearStart.AddYears(1);
        var requests = await db.Requests
            .Where(r => r.RequesterId == userId && r.Type == RequestType.Leave
                        && (r.Status == RequestStatus.Approved || r.Status == RequestStatus.Pending)
                        && r.StartDate >= yearStart && r.StartDate < yearEnd)
            .Select(r => new { r.LeaveTypeId, r.Status, r.Days })
            .ToListAsync();

        return types.Select(t =>
        {
            var entitlement = overrides.TryGetValue(t.Id, out var o) ? o : t.DefaultDays;
            var used = requests.Where(r => r.LeaveTypeId == t.Id && r.Status == RequestStatus.Approved).Sum(r => r.Days ?? 0);
            var pending = requests.Where(r => r.LeaveTypeId == t.Id && r.Status == RequestStatus.Pending).Sum(r => r.Days ?? 0);
            return new LeaveBalance(t.Id, t.Name, entitlement, used, pending, entitlement - used - pending);
        }).ToList();
    }
}
