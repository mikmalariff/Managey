using System.Globalization;
using Managey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Services;

/// <summary>General payroll options. Contribution rules live in <see cref="ContributionScheme"/>.</summary>
public class PayrollSettings(AppDbContext db)
{
    public const string Prefix = "Payroll.";

    public static readonly Dictionary<string, string> Defaults = new()
    {
        // Used to work out a daily rate for unpaid leave: basic salary ÷ days per month.
        ["DaysPerMonth"] = "26",
    };

    public async Task<Dictionary<string, decimal>> LoadAsync()
    {
        var stored = await db.Settings.Where(s => s.Key.StartsWith(Prefix)).ToDictionaryAsync(s => s.Key[Prefix.Length..], s => s.Value);
        return Defaults.ToDictionary(
            d => d.Key,
            d => decimal.TryParse(stored.GetValueOrDefault(d.Key), CultureInfo.InvariantCulture, out var v) ? v : decimal.Parse(d.Value, CultureInfo.InvariantCulture));
    }
}

public class PayrollService(AppDbContext db, LeaveService leave, PayrollSettings settings)
{
    /// <summary>Builds a payslip for every active employee with a basic salary. Replaces existing draft slips.</summary>
    public async Task GenerateAsync(PayrollRun run)
    {
        var daysPerMonth = (await settings.LoadAsync())["DaysPerMonth"];
        var monthStart = new DateTime(run.Year, run.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        db.Payslips.RemoveRange(await db.Payslips.Where(p => p.PayrollRunId == run.Id).ToListAsync());
        await db.SaveChangesAsync();

        var employees = await db.Users
            .Include(u => u.Department)
            .Include(u => u.Contributions)
            .Where(u => u.IsActive && u.BasicSalary > 0)
            .OrderBy(u => u.EmployeeNo)
            .ToListAsync();
        var ids = employees.Select(e => e.Id).ToList();
        var schemes = await LoadSchemesAsync();

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
                PassportNo = e.PassportNo,
                TaxNo = e.TaxNo,
                BankName = e.BankName,
                BankAccountNo = e.BankAccountNo,
                BasicSalary = e.BasicSalary ?? 0,
            };

            foreach (var item in recurring.Where(i => i.UserId == e.Id))
                slip.Lines.Add(new PayslipLine
                {
                    Kind = item.Kind, Name = item.Name, Amount = item.Amount, Statutory = item.Statutory, Source = PayLineSource.Recurring,
                });

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
            if (unpaidDays > 0 && daysPerMonth > 0)
                slip.Lines.Add(new PayslipLine
                {
                    Kind = PayItemKind.Deduction, Name = $"Unpaid leave ({unpaidDays} day(s))",
                    Amount = Math.Round(slip.BasicSalary / daysPerMonth * unpaidDays, 2),
                    Statutory = true, Source = PayLineSource.UnpaidLeave,
                });

            Calculate(slip, schemes, e.Contributions, monthStart);
            db.Payslips.Add(slip);
        }
    }

    /// <summary>Recalculates one payslip after HR edits it, using the employee's current scheme memberships.</summary>
    public async Task RecalculateAsync(Payslip slip, int year, int month)
    {
        var memberships = await db.EmployeeContributions.Where(c => c.UserId == slip.UserId).ToListAsync();
        Calculate(slip, await LoadSchemesAsync(), memberships, new DateTime(year, month, 1));
    }

    private Task<List<ContributionScheme>> LoadSchemesAsync() =>
        db.ContributionSchemes.Include(s => s.Bands).Where(s => s.IsActive).OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync();

    /// <summary>Recomputes totals, and the contributions unless HR entered them by hand.</summary>
    public static void Calculate(Payslip slip, List<ContributionScheme> schemes, IEnumerable<EmployeeContribution> memberships, DateTime month)
    {
        var earnings = slip.Lines.Where(l => l.Kind == PayItemKind.Earning).ToList();
        var deductions = slip.Lines.Where(l => l.Kind == PayItemKind.Deduction).ToList();

        slip.GrossPay = slip.BasicSalary + earnings.Sum(l => l.Amount);
        slip.StatutoryWage = Math.Max(0,
            slip.BasicSalary + earnings.Where(l => l.Statutory).Sum(l => l.Amount) - deductions.Where(l => l.Statutory).Sum(l => l.Amount));

        if (!slip.StatutoryOverride)
        {
            slip.Contributions.Clear();
            var byScheme = memberships.ToDictionary(m => m.SchemeId);
            foreach (var scheme in schemes)
            {
                if (!byScheme.TryGetValue(scheme.Id, out var member)) continue;
                if (scheme.EffectiveFrom is { } from && month < new DateTime(from.Year, from.Month, 1)) continue;
                var (employee, employer) = ContributionCalculator.Compute(scheme, slip.StatutoryWage, member);
                if (employee == 0 && employer == 0) continue;
                slip.Contributions.Add(new PayslipContribution
                {
                    SchemeId = scheme.Id, Name = scheme.Name, Code = scheme.Code, MemberNo = member.MemberNo,
                    Employee = employee, Employer = employer, SortOrder = scheme.SortOrder,
                });
            }
        }

        slip.TotalDeductions = deductions.Sum(l => l.Amount) + slip.Contributions.Sum(c => c.Employee);
        slip.EmployerContributions = slip.Contributions.Sum(c => c.Employer);
        slip.NetPay = slip.GrossPay - slip.TotalDeductions;
    }
}

public static class ContributionCalculator
{
    public static (decimal Employee, decimal Employer) Compute(ContributionScheme s, decimal wage, EmployeeContribution? member)
    {
        var capped = s.WageCeiling is > 0 ? Math.Min(wage, s.WageCeiling.Value) : wage;
        switch (s.Method)
        {
            case ContributionMethod.Percentage:
                return (Round(capped * s.EmployeeRate / 100, s.Rounding), Round(capped * s.EmployerRate / 100, s.Rounding));

            case ContributionMethod.RateBands:
            {
                if (FindBand(s, wage) is not { } band) return (0, 0);
                var basis = band.Step > 0 ? Math.Ceiling(capped / band.Step) * band.Step : capped;
                return (Round(basis * band.Employee / 100, s.Rounding), Round(basis * band.Employer / 100, s.Rounding));
            }

            case ContributionMethod.AmountBands:
                return FindBand(s, capped) is { } row ? (row.Employee, row.Employer) : (0, 0);

            case ContributionMethod.Fixed:
                return (s.EmployeeRate, s.EmployerRate);

            case ContributionMethod.PerEmployee:
                return (member?.EmployeeAmount ?? 0, member?.EmployerAmount ?? 0);

            default:
                return (0, 0);
        }
    }

    /// <summary>The band whose range holds the wage; wages above the last band use the last band; no wages, no band.</summary>
    public static ContributionBand? FindBand(ContributionScheme s, decimal wage)
    {
        if (wage <= 0 || s.Bands.Count == 0) return null;
        var bands = s.Bands.OrderBy(b => b.UpTo).ToList();
        return bands.FirstOrDefault(b => wage <= b.UpTo) ?? bands[^1];
    }

    private static decimal Round(decimal value, ContributionRounding rounding)
    {
        var cents = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return rounding switch
        {
            ContributionRounding.NearestWhole => Math.Round(cents, 0, MidpointRounding.AwayFromZero),
            ContributionRounding.UpWhole => Math.Ceiling(cents),
            _ => cents,
        };
    }
}
