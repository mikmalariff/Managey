using Managey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Services;

/// <summary>
/// Statutory rates, stored in Settings so HR can update them when the law changes.
/// Percentages are entered as e.g. 11 for 11%.
/// </summary>
public record PayrollRates(
    decimal EpfEmployee, decimal EpfEmployerLow, decimal EpfEmployerHigh, decimal EpfEmployerThreshold,
    decimal SocsoEmployee, decimal SocsoEmployer, decimal EisEmployee, decimal EisEmployer,
    decimal SocsoEisCeiling, decimal DaysPerMonth);

public class PayrollSettings(AppDbContext db)
{
    public const string Prefix = "Payroll.";

    public static readonly Dictionary<string, string> Defaults = new()
    {
        ["EpfEmployee"] = "11",
        ["EpfEmployerLow"] = "13",
        ["EpfEmployerHigh"] = "12",
        ["EpfEmployerThreshold"] = "5000",
        ["SocsoEmployee"] = "0.5",
        ["SocsoEmployer"] = "1.75",
        ["EisEmployee"] = "0.2",
        ["EisEmployer"] = "0.2",
        ["SocsoEisCeiling"] = "6000",
        ["DaysPerMonth"] = "26",
    };

    public async Task<Dictionary<string, decimal>> LoadRawAsync()
    {
        var stored = await db.Settings.Where(s => s.Key.StartsWith(Prefix)).ToDictionaryAsync(s => s.Key[Prefix.Length..], s => s.Value);
        return Defaults.ToDictionary(
            d => d.Key,
            d => decimal.TryParse(stored.GetValueOrDefault(d.Key), out var v) ? v : decimal.Parse(d.Value));
    }

    public async Task<PayrollRates> LoadAsync()
    {
        var r = await LoadRawAsync();
        return new PayrollRates(r["EpfEmployee"], r["EpfEmployerLow"], r["EpfEmployerHigh"], r["EpfEmployerThreshold"],
            r["SocsoEmployee"], r["SocsoEmployer"], r["EisEmployee"], r["EisEmployer"], r["SocsoEisCeiling"], r["DaysPerMonth"]);
    }
}

public class PayrollService(AppDbContext db, LeaveService leave, PayrollSettings settings)
{
    /// <summary>Builds a payslip for every active employee with a basic salary. Replaces existing draft slips.</summary>
    public async Task GenerateAsync(PayrollRun run)
    {
        var rates = await settings.LoadAsync();
        var monthStart = new DateTime(run.Year, run.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        db.Payslips.RemoveRange(await db.Payslips.Where(p => p.PayrollRunId == run.Id).ToListAsync());
        await db.SaveChangesAsync();

        var employees = await db.Users
            .Include(u => u.Department)
            .Where(u => u.IsActive && u.BasicSalary > 0)
            .OrderBy(u => u.EmployeeNo)
            .ToListAsync();
        var ids = employees.Select(e => e.Id).ToList();

        var recurring = await db.EmployeePayItems.Where(i => ids.Contains(i.UserId)).ToListAsync();

        // Approved claims not already on another payslip, approved by the end of this month.
        var paidClaimIds = db.PayslipLines.Where(l => l.RequestId != null).Select(l => l.RequestId!.Value);
        var cutoff = monthEnd.AddDays(1);
        var claims = await db.Requests
            .Where(r => ids.Contains(r.RequesterId) && r.Type == RequestType.Claim && r.Status == RequestStatus.Approved
                        && r.CompletedAt < cutoff && !paidClaimIds.Contains(r.Id))
            .ToListAsync();

        var unpaidLeave = await db.Requests
            .Where(r => ids.Contains(r.RequesterId) && r.Type == RequestType.Leave && r.Status == RequestStatus.Approved
                        && r.LeaveType!.IsUnpaid && r.StartDate <= monthEnd && r.EndDate >= monthStart)
            .ToListAsync();

        foreach (var e in employees)
        {
            var slip = new Payslip
            {
                PayrollRunId = run.Id,
                UserId = e.Id,
                EmployeeNo = e.EmployeeNo,
                EmployeeName = e.FullName,
                Department = e.Department?.Name,
                Position = e.Position,
                IcNumber = e.IcNumber,
                EpfNo = e.EpfNo,
                SocsoNo = e.SocsoNo,
                TaxNo = e.TaxNo,
                BankName = e.BankName,
                BankAccountNo = e.BankAccountNo,
                BasicSalary = e.BasicSalary ?? 0,
                Pcb = e.MonthlyPcb ?? 0,
            };

            foreach (var item in recurring.Where(i => i.UserId == e.Id))
                slip.Lines.Add(new PayslipLine { Kind = item.Kind, Name = item.Name, Amount = item.Amount, Statutory = item.Statutory, Source = PayLineSource.Recurring });

            foreach (var claim in claims.Where(c => c.RequesterId == e.Id))
                slip.Lines.Add(new PayslipLine
                {
                    Kind = PayItemKind.Earning, Name = $"Claim {claim.RefNo}: {claim.Title}", Amount = claim.Amount ?? 0,
                    Statutory = false, Source = PayLineSource.Claim, RequestId = claim.Id,
                });

            decimal unpaidDays = 0;
            foreach (var l in unpaidLeave.Where(l => l.RequesterId == e.Id))
            {
                var from = l.StartDate!.Value < monthStart ? monthStart : l.StartDate.Value;
                var to = l.EndDate!.Value > monthEnd ? monthEnd : l.EndDate.Value;
                unpaidDays += await leave.CountWorkingDaysAsync(from, to, l.HalfDay);
            }
            if (unpaidDays > 0 && rates.DaysPerMonth > 0)
                slip.Lines.Add(new PayslipLine
                {
                    Kind = PayItemKind.Deduction, Name = $"Unpaid leave ({unpaidDays} day(s))",
                    Amount = Math.Round(slip.BasicSalary / rates.DaysPerMonth * unpaidDays, 2),
                    Statutory = true, Source = PayLineSource.UnpaidLeave,
                });

            Calculate(slip, rates);
            db.Payslips.Add(slip);
        }
    }

    /// <summary>Recomputes totals, and EPF/SOCSO/EIS unless HR entered them by hand.</summary>
    public static void Calculate(Payslip slip, PayrollRates rates)
    {
        var earnings = slip.Lines.Where(l => l.Kind == PayItemKind.Earning).ToList();
        var deductions = slip.Lines.Where(l => l.Kind == PayItemKind.Deduction).ToList();

        slip.GrossPay = slip.BasicSalary + earnings.Sum(l => l.Amount);
        slip.StatutoryWage = Math.Max(0,
            slip.BasicSalary + earnings.Where(l => l.Statutory).Sum(l => l.Amount) - deductions.Where(l => l.Statutory).Sum(l => l.Amount));

        if (!slip.StatutoryOverride)
        {
            var wage = slip.StatutoryWage;
            slip.EpfEmployee = Epf(wage, rates.EpfEmployee);
            slip.EpfEmployer = Epf(wage, wage <= rates.EpfEmployerThreshold ? rates.EpfEmployerLow : rates.EpfEmployerHigh);
            var capped = Math.Min(wage, rates.SocsoEisCeiling);
            slip.SocsoEmployee = Percent(capped, rates.SocsoEmployee);
            slip.SocsoEmployer = Percent(capped, rates.SocsoEmployer);
            slip.EisEmployee = Percent(capped, rates.EisEmployee);
            slip.EisEmployer = Percent(capped, rates.EisEmployer);
        }

        slip.TotalDeductions = deductions.Sum(l => l.Amount) + slip.EpfEmployee + slip.SocsoEmployee + slip.EisEmployee + slip.Pcb;
        slip.NetPay = slip.GrossPay - slip.TotalDeductions;
    }

    /// <summary>
    /// EPF follows the Third Schedule approach: wages are rounded up to their band (RM20 up to RM5,000,
    /// RM100 up to RM20,000), the rate is applied, and the result is rounded up to the next ringgit.
    /// </summary>
    public static decimal Epf(decimal wage, decimal ratePercent)
    {
        if (wage <= 10 || ratePercent <= 0) return 0;
        var basis = wage switch
        {
            <= 5000 => Math.Ceiling(wage / 20) * 20,
            <= 20000 => Math.Ceiling(wage / 100) * 100,
            _ => wage,
        };
        return Math.Ceiling(basis * ratePercent / 100);
    }

    private static decimal Percent(decimal amount, decimal ratePercent) =>
        Math.Round(amount * ratePercent / 100, 2, MidpointRounding.AwayFromZero);
}
