using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

[ApiController]
[Route("api/payroll")]
[Authorize(Roles = Roles.AdminOrHR)]
public class PayrollController(AppDbContext db, AuditService audit, PayrollService payroll, PayrollSettings settings) : ControllerBase
{
    public record RunInput(int Year, int Month);

    public record LineInput(PayItemKind Kind, string Name, decimal Amount, bool Statutory, PayLineSource Source, int? RequestId);

    public record ContributionInput(int? SchemeId, string Name, string Code, string? MemberNo, decimal Employee, decimal Employer);

    public record PayslipInput(decimal BasicSalary, List<LineInput> Lines, bool StatutoryOverride, List<ContributionInput> Contributions);

    [HttpGet("runs")]
    public async Task<IActionResult> Runs()
    {
        var runs = await db.PayrollRuns
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => new { r.Id, r.Year, r.Month, r.Status, r.CreatedAt, r.FinalizedAt, NetPays = r.Payslips.Select(p => p.NetPay).ToList() })
            .ToListAsync();
        // Summed in memory because SQLite cannot sum decimals.
        return Ok(runs.Select(r => new { r.Id, r.Year, r.Month, r.Status, r.CreatedAt, r.FinalizedAt, Employees = r.NetPays.Count, NetPay = r.NetPays.Sum() }));
    }

    [HttpPost("runs")]
    public async Task<IActionResult> Create(RunInput input)
    {
        if (input.Month is < 1 or > 12 || input.Year is < 2000 or > 2100) return BadRequest(new { message = "Choose a valid month." });
        if (await db.PayrollRuns.AnyAsync(r => r.Year == input.Year && r.Month == input.Month))
            return BadRequest(new { message = "Payroll for that month already exists." });

        var run = new PayrollRun { Year = input.Year, Month = input.Month, CreatedById = User.UserId() };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync();
        await payroll.GenerateAsync(run);
        audit.Log("Create", nameof(PayrollRun), run.Id, $"{input.Year}-{input.Month:D2}");
        await db.SaveChangesAsync();
        return Ok(new { run.Id });
    }

    [HttpGet("runs/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var run = await LoadRunAsync(id);
        if (run is null) return NotFound();
        var slips = run.Payslips.OrderBy(p => p.EmployeeNo).ToList();
        var needMemberNo = await db.ContributionSchemes.Where(c => c.MemberNoRequired).Select(c => c.Code).ToListAsync();
        var all = slips.SelectMany(p => p.Contributions).ToList();
        var codes = all.GroupBy(c => c.Code).OrderBy(g => g.Min(c => c.SortOrder)).ThenBy(g => g.Key)
            .Select(g => new { Code = g.Key, Name = g.First().Name, Employee = g.Sum(c => c.Employee), Employer = g.Sum(c => c.Employer) })
            .ToList();

        return Ok(new
        {
            run.Id, run.Year, run.Month, run.Status, run.CreatedAt, run.FinalizedAt,
            Schemes = codes,
            Payslips = slips.Select(p => new
            {
                p.Id, p.UserId, p.EmployeeNo, p.EmployeeName, p.Department, p.GrossPay, p.TotalDeductions, p.EmployerContributions, p.NetPay,
                Contributions = p.Contributions.Select(c => new { c.Code, c.Employee, c.Employer }),
                MissingBank = string.IsNullOrEmpty(p.BankAccountNo),
                MissingId = string.IsNullOrEmpty(p.IcNumber) && string.IsNullOrEmpty(p.PassportNo),
                MissingMemberNo = p.Contributions
                    .Where(c => string.IsNullOrWhiteSpace(c.MemberNo) && needMemberNo.Contains(c.Code))
                    .Select(c => c.Code).Distinct(),
            }),
            Totals = new
            {
                GrossPay = slips.Sum(p => p.GrossPay),
                NetPay = slips.Sum(p => p.NetPay),
                Employee = all.Sum(c => c.Employee),
                Employer = all.Sum(c => c.Employer),
                EmployerCost = slips.Sum(p => p.GrossPay + p.EmployerContributions),
            },
            // Country-specific upload files, offered when the Malaysia preset's schemes are in use.
            Exports = new
            {
                Kwsp = all.Any(c => StatutoryFiles.EpfCodes.Contains(c.Code)),
                Perkeso = all.Any(c => StatutoryFiles.SocsoCodes.Contains(c.Code) || c.Code is "EIS" or "SKBBK"),
            },
        });
    }

    [HttpPost("runs/{id:int}/regenerate")]
    public async Task<IActionResult> Regenerate(int id)
    {
        var run = await db.PayrollRuns.FindAsync(id);
        if (run is null) return NotFound();
        if (run.Status != PayrollStatus.Draft) return BadRequest(new { message = "A finalised payroll cannot be changed." });
        await payroll.GenerateAsync(run);
        audit.Log("Regenerate", nameof(PayrollRun), id, $"{run.Year}-{run.Month:D2}");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("runs/{id:int}/finalize")]
    public async Task<IActionResult> Finalize(int id)
    {
        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return NotFound();
        if (run.Status != PayrollStatus.Draft) return BadRequest(new { message = "Already finalised." });
        if (run.Payslips.Count == 0) return BadRequest(new { message = "There are no payslips to finalise." });
        run.Status = PayrollStatus.Finalized;
        run.FinalizedAt = DateTime.UtcNow;
        run.FinalizedById = User.UserId();
        audit.Log("Finalize", nameof(PayrollRun), id, $"{run.Year}-{run.Month:D2}");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("runs/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var run = await db.PayrollRuns.FindAsync(id);
        if (run is null) return NotFound();
        if (run.Status != PayrollStatus.Draft) return BadRequest(new { message = "A finalised payroll cannot be deleted." });
        db.PayrollRuns.Remove(run);
        audit.Log("Delete", nameof(PayrollRun), id, $"{run.Year}-{run.Month:D2}");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("payslips/{id:int}")]
    public async Task<IActionResult> UpdatePayslip(int id, PayslipInput input)
    {
        var slip = await db.Payslips.Include(p => p.Lines).Include(p => p.Contributions).Include(p => p.PayrollRun).FirstOrDefaultAsync(p => p.Id == id);
        if (slip is null) return NotFound();
        if (slip.PayrollRun!.Status != PayrollStatus.Draft) return BadRequest(new { message = "A finalised payslip cannot be changed." });
        if (input.BasicSalary < 0 || input.Lines.Any(l => l.Amount < 0 || string.IsNullOrWhiteSpace(l.Name)))
            return BadRequest(new { message = "Every line needs a name and an amount of zero or more." });
        if (input.StatutoryOverride && input.Contributions.Any(c => c.Employee < 0 || c.Employer < 0 || string.IsNullOrWhiteSpace(c.Name)))
            return BadRequest(new { message = "Every contribution needs a name and amounts of zero or more." });

        // Keep the claim link only for claims already on this slip, so a claim cannot be paid twice.
        var claimIds = slip.Lines.Where(l => l.RequestId != null).Select(l => l.RequestId).ToHashSet();
        db.PayslipLines.RemoveRange(slip.Lines);
        slip.Lines = input.Lines.Select(l => new PayslipLine
        {
            Kind = l.Kind,
            Name = l.Name.Trim(),
            Amount = Math.Round(l.Amount, 2),
            Statutory = l.Statutory,
            Source = claimIds.Contains(l.RequestId) ? l.Source : l.Source == PayLineSource.Claim ? PayLineSource.Manual : l.Source,
            RequestId = claimIds.Contains(l.RequestId) ? l.RequestId : null,
        }).ToList();

        slip.BasicSalary = Math.Round(input.BasicSalary, 2);
        slip.StatutoryOverride = input.StatutoryOverride;
        if (input.StatutoryOverride)
        {
            db.PayslipContributions.RemoveRange(slip.Contributions);
            slip.Contributions = input.Contributions.Select((c, i) => new PayslipContribution
            {
                SchemeId = c.SchemeId, Name = c.Name.Trim(), Code = c.Code.Trim(), MemberNo = c.MemberNo,
                Employee = Math.Round(c.Employee, 2), Employer = Math.Round(c.Employer, 2), SortOrder = i,
            }).ToList();
        }
        await payroll.RecalculateAsync(slip, slip.PayrollRun.Year, slip.PayrollRun.Month);
        audit.Log("Update", nameof(Payslip), id, slip.EmployeeName);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Simple bank upload file: name, account, amount. Most banks accept a CSV like this or can map it.</summary>
    [HttpGet("runs/{id:int}/bank.csv")]
    public async Task<IActionResult> BankFile(int id)
    {
        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return NotFound();
        var csv = new StringBuilder("Employee No,Name,ID No,Bank,Account No,Amount\n");
        foreach (var p in run.Payslips.OrderBy(p => p.EmployeeNo))
            csv.AppendJoin(',', Csv(p.EmployeeNo), Csv(p.EmployeeName), Csv(p.IcNumber ?? p.PassportNo), Csv(p.BankName), Csv(p.BankAccountNo),
                p.NetPay.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
        audit.Log("Export", nameof(PayrollRun), id, "Bank file");
        await db.SaveChangesAsync();
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv", $"bank-{run.Year}-{run.Month:D2}.csv");
    }

    /// <summary>KWSP e-Caruman upload (Borang A) for i-Akaun. The contribution month is the month after the wage month.</summary>
    [HttpGet("runs/{id:int}/kwsp.csv")]
    public async Task<IActionResult> KwspFile(int id)
    {
        var run = await LoadRunAsync(id);
        if (run is null) return NotFound();
        var csv = StatutoryFiles.Kwsp(run.Payslips);
        audit.Log("Export", nameof(PayrollRun), id, "KWSP file");
        await db.SaveChangesAsync();
        var contribution = new DateTime(run.Year, run.Month, 1).AddMonths(1);
        return File(Encoding.ASCII.GetBytes(csv), "text/csv", $"KWSP-{contribution:yyyyMM}.csv");
    }

    /// <summary>PERKESO ASSIST combined SOCSO + EIS (+ SKBBK) contribution text file.</summary>
    [HttpGet("runs/{id:int}/perkeso.txt")]
    public async Task<IActionResult> PerkesoFile(int id)
    {
        var run = await LoadRunAsync(id);
        if (run is null) return NotFound();
        var employerCode = await db.ContributionSchemes.Where(s => s.Code == "SOCSO").Select(s => s.EmployerRef).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(employerCode))
            return BadRequest(new { message = "Enter the PERKESO employer code on the SOCSO scheme under Setup → Payroll first." });
        var regNo = await db.Settings.Where(s => s.Key == "CompanyRegNo").Select(s => s.Value).FirstOrDefaultAsync();
        var text = StatutoryFiles.Perkeso(run, employerCode.Trim(), regNo?.Trim());
        audit.Log("Export", nameof(PayrollRun), id, "PERKESO file");
        await db.SaveChangesAsync();
        // ASSIST rejects a file name it has seen before, so stamp the time.
        return File(Encoding.ASCII.GetBytes(text), "text/plain", $"PERKESO-{run.Year}{run.Month:D2}-{DateTime.Now:yyyyMMddHHmm}.txt");
    }

    /// <summary>Generic per-scheme report for any country: ID, member no., wages and amounts.</summary>
    [HttpGet("runs/{id:int}/contributions/{code}.csv")]
    public async Task<IActionResult> ContributionFile(int id, string code)
    {
        var run = await LoadRunAsync(id);
        if (run is null) return NotFound();
        var csv = StatutoryFiles.Contributions(run.Payslips, code);
        audit.Log("Export", nameof(PayrollRun), id, $"{code} contributions");
        await db.SaveChangesAsync();
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), "text/csv", $"{code}-{run.Year}-{run.Month:D2}.csv");
    }

    [HttpGet("runs/{id:int}/export.xlsx")]
    public async Task<IActionResult> Export(int id)
    {
        var run = await LoadRunAsync(id);
        if (run is null) return NotFound();
        var slips = run.Payslips.OrderBy(p => p.EmployeeNo).ToList();
        var codes = slips.SelectMany(p => p.Contributions).GroupBy(c => c.Code).OrderBy(g => g.Min(c => c.SortOrder)).Select(g => g.Key).ToList();

        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet($"Payroll {run.Year}-{run.Month:D2}");
        var headers = new List<string> { "Employee No", "Name", "ID No", "Department", "Basic", "Gross" };
        foreach (var code in codes) { headers.Add($"{code} (Employee)"); headers.Add($"{code} (Employer)"); }
        headers.AddRange(["Total Deductions", "Net Pay", "Employer Contributions", "Tax No", "Bank", "Account No"]);
        for (var i = 0; i < headers.Count; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var p in slips)
        {
            var values = new List<object?> { p.EmployeeNo, p.EmployeeName, p.IcNumber ?? p.PassportNo, p.Department, p.BasicSalary, p.GrossPay };
            foreach (var code in codes)
            {
                var c = p.Contributions.Where(x => x.Code == code).ToList();
                values.Add(c.Sum(x => x.Employee));
                values.Add(c.Sum(x => x.Employer));
            }
            values.AddRange([p.TotalDeductions, p.NetPay, p.EmployerContributions, p.TaxNo, p.BankName, p.BankAccountNo]);
            for (var i = 0; i < values.Count; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                if (values[i] is decimal d) { cell.Value = d; cell.Style.NumberFormat.Format = "#,##0.00"; }
                else cell.Value = values[i]?.ToString() ?? "";
            }
            row++;
        }
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"payroll-{run.Year}-{run.Month:D2}.xlsx");
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() => Ok(await settings.LoadAsync());

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(Dictionary<string, decimal> values)
    {
        foreach (var (name, value) in values)
        {
            if (!PayrollSettings.Defaults.ContainsKey(name)) continue;
            if (value < 0) return BadRequest(new { message = $"{name} cannot be negative." });
            var key = PayrollSettings.Prefix + name;
            var row = await db.Settings.FindAsync(key);
            var text = value.ToString(CultureInfo.InvariantCulture);
            if (row is null) db.Settings.Add(new Setting { Key = key, Value = text });
            else row.Value = text;
        }
        audit.Log("Update", nameof(Setting), null, "Payroll settings");
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<PayrollRun?> LoadRunAsync(int id) => db.PayrollRuns
        .Include(r => r.Payslips).ThenInclude(p => p.Contributions)
        .AsSplitQuery()
        .FirstOrDefaultAsync(r => r.Id == id);

    private static string Csv(string? s) =>
        s is null ? "" : s.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}

/// <summary>Payslips as employees see them. HR can open any; employees only their own finalised slips.</summary>
[ApiController]
[Route("api/payslips")]
[Authorize]
public class PayslipsController(AppDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var me = User.UserId();
        return Ok(await db.Payslips
            .Where(p => p.UserId == me && p.PayrollRun!.Status == PayrollStatus.Finalized)
            .OrderByDescending(p => p.PayrollRun!.Year).ThenByDescending(p => p.PayrollRun!.Month)
            .Select(p => new { p.Id, p.PayrollRun!.Year, p.PayrollRun.Month, p.GrossPay, p.TotalDeductions, p.NetPay })
            .ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await db.Payslips.Include(x => x.Lines).Include(x => x.Contributions).Include(x => x.PayrollRun).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        var isOwner = p.UserId == User.UserId() && p.PayrollRun!.Status == PayrollStatus.Finalized;
        if (!isOwner && !User.IsAdminOrHR()) return NotFound();

        var company = await db.Settings.Where(s => s.Key == "CompanyName").Select(s => s.Value).FirstOrDefaultAsync();
        return Ok(new
        {
            p.Id, p.PayrollRunId, p.PayrollRun!.Year, p.PayrollRun.Month, RunStatus = p.PayrollRun.Status, CompanyName = company,
            p.UserId, p.EmployeeNo, p.EmployeeName, p.Department, p.Position, p.IcNumber, p.PassportNo, p.TaxNo, p.BankName, p.BankAccountNo,
            p.BasicSalary, p.GrossPay, p.StatutoryWage, p.StatutoryOverride,
            p.TotalDeductions, p.EmployerContributions, p.NetPay,
            Lines = p.Lines.OrderBy(l => l.Kind).ThenBy(l => l.Id).Select(l => new { l.Kind, l.Name, l.Amount, l.Statutory, l.Source, l.RequestId }),
            Contributions = p.Contributions.OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .Select(c => new { c.SchemeId, c.Name, c.Code, c.MemberNo, c.Employee, c.Employer }),
        });
    }
}
