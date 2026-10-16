using Managey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Services;

public record LeaveBalance(int LeaveTypeId, string LeaveType, decimal Entitlement, decimal Used, decimal Pending, decimal Remaining);

public class LeaveService(AppDbContext db)
{
    public const string DefaultWorkDays = "1,2,3,4,5";

    /// <summary>Parses "1,2,3,4,5" (Sunday = 0) into days of the week.</summary>
    public static HashSet<DayOfWeek> ParseWorkDays(string? value) =>
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => int.TryParse(v, out var n) && n is >= 0 and <= 6 ? (DayOfWeek?)n : null)
            .Where(d => d is not null).Select(d => d!.Value).ToHashSet();

    /// <summary>Counts the company's working days between two dates, inclusive, skipping public holidays.</summary>
    public async Task<decimal> CountWorkingDaysAsync(DateTime start, DateTime end, bool halfDay)
    {
        var setting = await db.Settings.Where(s => s.Key == "WorkDays").Select(s => s.Value).FirstOrDefaultAsync();
        var workDays = ParseWorkDays(setting ?? DefaultWorkDays);
        var holidays = (await db.Holidays.Where(h => h.Date >= start.Date && h.Date <= end.Date).Select(h => h.Date).ToListAsync())
            .Select(d => d.Date).ToHashSet();
        decimal days = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
        {
            if (workDays.Contains(d.DayOfWeek) && !holidays.Contains(d)) days++;
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
