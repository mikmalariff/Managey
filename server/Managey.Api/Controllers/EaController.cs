using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

/// <summary>
/// Borang EA (C.P.8A): yearly statement of remuneration, built from the year's finalised payslips.
/// HR can open anyone's; employees only their own.
/// </summary>
[ApiController]
[Route("api/ea")]
[Authorize]
public class EaController(AppDbContext db) : ControllerBase
{
    public static readonly string[] EmployerKeys =
        ["CompanyName", "CompanyRegNo", "CompanyAddress", "CompanyPhone", "EmployerTaxNo", "EaSignatoryName", "EaSignatoryPosition"];

    /// <summary>Years in which the signed-in employee has finalised payslips.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> MyYears()
    {
        var me = User.UserId();
        return Ok(await db.Payslips
            .Where(p => p.UserId == me && p.PayrollRun!.Status == PayrollStatus.Finalized)
            .Select(p => p.PayrollRun!.Year).Distinct().OrderByDescending(y => y).ToListAsync());
    }

    [HttpGet("{year:int}")]
    [Authorize(Roles = Roles.AdminOrHR)]
    public async Task<IActionResult> List(int year)
    {
        var slips = await FinalisedAsync(year, null);
        return Ok(slips.GroupBy(p => p.UserId).Select(g =>
        {
            var ea = Build(g.ToList());
            var last = g.OrderBy(p => p.PayrollRun!.Month).Last();
            return new
            {
                UserId = g.Key, last.EmployeeNo, last.EmployeeName, last.TaxNo, Months = g.Count(),
                ea.TotalIncome, ea.Pcb, ea.Epf,
            };
        }).OrderBy(x => x.EmployeeNo));
    }

    [HttpGet("{year:int}/{userId:int}")]
    public async Task<IActionResult> Get(int year, int userId)
    {
        if (userId != User.UserId() && !User.IsAdminOrHR()) return NotFound();
        var slips = await FinalisedAsync(year, userId);
        if (slips.Count == 0) return NotFound();

        var user = await db.Users.FindAsync(userId);
        var last = slips.OrderBy(p => p.PayrollRun!.Month).Last();
        var employer = await db.Settings.Where(s => EmployerKeys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, s => s.Value);
        var joinedThisYear = user?.JoinDate?.Year == year ? user.JoinDate : null;
        var ea = Build(slips);

        return Ok(new
        {
            Year = year,
            SerialNo = $"{year}/{last.EmployeeNo}",
            Employer = employer,
            Employee = new
            {
                Name = last.EmployeeName, last.Position, last.EmployeeNo, last.IcNumber, last.PassportNo,
                last.EpfNo, last.SocsoNo, last.TaxNo, JoinDate = joinedThisYear,
                Months = slips.Count,
            },
            Amounts = ea,
        });
    }

    public record EaAmounts(
        decimal Salary, decimal Bonus, decimal Allowances, decimal TotalIncome,
        decimal Pcb, decimal Cp38, decimal Zakat, decimal Epf, decimal Perkeso, decimal Exempt);

    internal static EaAmounts Build(List<Payslip> slips)
    {
        decimal Sum(PayItemKind kind, PayCategory category) =>
            slips.SelectMany(p => p.Lines).Where(l => l.Kind == kind && l.Category == category).Sum(l => l.Amount);

        var salary = slips.Sum(p => p.BasicSalary) + Sum(PayItemKind.Earning, PayCategory.Salary) - Sum(PayItemKind.Deduction, PayCategory.Salary);
        var bonus = Sum(PayItemKind.Earning, PayCategory.Bonus);
        var allowances = Sum(PayItemKind.Earning, PayCategory.Allowance);
        return new EaAmounts(
            salary, bonus, allowances, salary + bonus + allowances,
            slips.Sum(p => p.Pcb),
            Sum(PayItemKind.Deduction, PayCategory.Cp38),
            Sum(PayItemKind.Deduction, PayCategory.Zakat),
            slips.Sum(p => p.EpfEmployee),
            slips.Sum(p => p.SocsoEmployee + p.EisEmployee + p.SkbbkEmployee),
            Sum(PayItemKind.Earning, PayCategory.Exempt));
    }

    private Task<List<Payslip>> FinalisedAsync(int year, int? userId) => db.Payslips
        .Include(p => p.Lines)
        .Include(p => p.PayrollRun)
        .Where(p => p.PayrollRun!.Year == year && p.PayrollRun.Status == PayrollStatus.Finalized && (userId == null || p.UserId == userId))
        .ToListAsync();
}
