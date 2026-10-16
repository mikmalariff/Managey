using System.Text;
using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController(AppDbContext db, AuditService audit) : ControllerBase
{
    public record ClockInput(string? Note);

    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var record = await db.Attendance.FirstOrDefaultAsync(a => a.UserId == User.UserId() && a.Date == DateTime.Today);
        return Ok(record is null ? null : (await ToRowsAsync([record]))[0]);
    }

    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn(ClockInput input)
    {
        var me = User.UserId();
        if (await db.Attendance.AnyAsync(a => a.UserId == me && a.Date == DateTime.Today))
            return BadRequest(new { message = "You have already clocked in today." });
        var record = new AttendanceRecord { UserId = me, Date = DateTime.Today, ClockIn = DateTime.UtcNow, Note = Clean(input.Note) };
        db.Attendance.Add(record);
        audit.Log("ClockIn", nameof(AttendanceRecord));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut(ClockInput input)
    {
        var record = await db.Attendance.FirstOrDefaultAsync(a => a.UserId == User.UserId() && a.Date == DateTime.Today);
        if (record is null) return BadRequest(new { message = "You have not clocked in today." });
        if (record.ClockOut is not null) return BadRequest(new { message = "You have already clocked out today." });
        record.ClockOut = DateTime.UtcNow;
        if (Clean(input.Note) is { } note) record.Note = record.Note is null ? note : $"{record.Note} / {note}";
        audit.Log("ClockOut", nameof(AttendanceRecord), record.Id);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// userId omitted: own records. userId set: that person (self, HR/admin, or their manager).
    /// everyone=true: all staff (HR/admin) or direct reports (managers).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? userId, [FromQuery] bool everyone = false)
    {
        var query = await ScopeAsync(from, to, userId, everyone);
        if (query is null) return Forbid();
        return Ok(await ToRowsAsync(await query.ToListAsync()));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? userId, [FromQuery] bool everyone = false)
    {
        var query = await ScopeAsync(from, to, userId, everyone);
        if (query is null) return Forbid();
        var rows = await ToRowsAsync(await query.ToListAsync());

        var csv = new StringBuilder("Date,Employee No,Name,Clock In,Clock Out,Hours,Late,Note\n");
        foreach (var r in rows)
        {
            csv.AppendJoin(',', r.Date.ToString("yyyy-MM-dd"), Csv(r.EmployeeNo), Csv(r.Name),
                r.ClockIn.ToLocalTime().ToString("HH:mm"), r.ClockOut?.ToLocalTime().ToString("HH:mm") ?? "",
                r.Hours?.ToString("0.00") ?? "", r.Late ? "Yes" : "No", Csv(r.Note));
            csv.Append('\n');
        }
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv", $"attendance-{DateTime.Today:yyyyMMdd}.csv");
    }

    public record AttendanceRow(int Id, int UserId, string EmployeeNo, string Name, DateTime Date,
        DateTime ClockIn, DateTime? ClockOut, decimal? Hours, bool Late, string? Note);

    private async Task<IQueryable<AttendanceRecord>?> ScopeAsync(DateTime? from, DateTime? to, int? userId, bool everyone)
    {
        var me = User.UserId();
        var start = (from ?? DateTime.Today.AddDays(-30)).Date;
        var end = (to ?? DateTime.Today).Date;
        var query = db.Attendance.Where(a => a.Date >= start && a.Date <= end);

        if (userId is { } id && id != me)
        {
            var allowed = User.IsAdminOrHR() || await db.Users.AnyAsync(u => u.Id == id && u.ManagerId == me);
            if (!allowed) return null;
            query = query.Where(a => a.UserId == id);
        }
        else if (everyone)
        {
            if (!User.IsAdminOrHR()) query = query.Where(a => a.User!.ManagerId == me || a.UserId == me);
        }
        else
        {
            query = query.Where(a => a.UserId == me);
        }
        return query.OrderByDescending(a => a.Date).ThenBy(a => a.User!.FullName);
    }

    private async Task<List<AttendanceRow>> ToRowsAsync(List<AttendanceRecord> records)
    {
        var startSetting = await db.Settings.Where(s => s.Key == "WorkStartTime").Select(s => s.Value).FirstOrDefaultAsync();
        var workStart = TimeOnly.TryParse(startSetting, out var t) ? t : new TimeOnly(9, 0);
        var ids = records.Select(r => r.UserId).Distinct().ToList();
        var users = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        return records.Select(r => new AttendanceRow(
            r.Id, r.UserId, users[r.UserId].EmployeeNo, users[r.UserId].FullName, r.Date, r.ClockIn, r.ClockOut,
            r.ClockOut is { } o ? Math.Round((decimal)(o - r.ClockIn).TotalHours, 2) : null,
            TimeOnly.FromDateTime(r.ClockIn.ToLocalTime()) > workStart.AddMinutes(1),
            r.Note)).ToList();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Csv(string? s) =>
        s is null ? "" : s.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
