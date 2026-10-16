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

    public record LineInput(PayItemKind Kind, string Name, decimal Amount, bool Statutory, PayCategory Category, PayLineSource Source, int? RequestId);

    public record PayslipInput(
        decimal BasicSalary, List<LineInput> Lines, bool StatutoryOverride,
        decimal EpfEmployee, decimal EpfEmployer, decimal SocsoEmployee, decimal SocsoEmployer,
        decimal EisEmployee, decimal EisEmployer, decimal SkbbkEmployee, decimal Pcb);

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
        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return NotFound();
        var slips = run.Payslips.OrderBy(p => p.EmployeeNo).ToList();
        return Ok(new
        {
            run.Id, run.Year, run.Month, run.Status, run.CreatedAt, run.FinalizedAt,
            Payslips = slips.Select(p => new
            {
                p.Id, p.UserId, p.EmployeeNo, p.EmployeeName, p.Department, p.GrossPay, p.TotalDeductions, p.NetPay,
                p.EpfEmployee, p.EpfEmployer, p.SocsoEmployee, p.SocsoEmployer, p.EisEmployee, p.EisEmployer, p.SkbbkEmployee, p.Pcb,
                MissingBank = string.IsNullOrEmpty(p.BankAccountNo),
                MissingIc = string.IsNullOrEmpty(p.IcNumber) && string.IsNullOrEmpty(p.PassportNo) && string.IsNullOrEmpty(p.SocsoNo),
                MissingEpfNo = string.IsNullOrEmpty(p.EpfNo) && p.EpfEmployee + p.EpfEmployer > 0,
            }),
            Totals = new
            {
                GrossPay = slips.Sum(p => p.GrossPay),
                NetPay = slips.Sum(p => p.NetPay),
                Epf = slips.Sum(p => p.EpfEmployee + p.EpfEmployer),
                Socso = slips.Sum(p => p.SocsoEmployee + p.SocsoEmployer + p.SkbbkEmployee),
                Eis = slips.Sum(p => p.EisEmployee + p.EisEmployer),
                Pcb = slips.Sum(p => p.Pcb),
                EmployerCost = slips.Sum(p => p.GrossPay + p.EpfEmployer + p.SocsoEmployer + p.EisEmployer),
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
        var slip = await db.Payslips.Include(p => p.Lines).Include(p => p.PayrollRun).FirstOrDefaultAsync(p => p.Id == id);
        if (slip is null) return NotFound();
        if (slip.PayrollRun!.Status != PayrollStatus.Draft) return BadRequest(new { message = "A finalised payslip cannot be changed." });
        if (input.BasicSalary < 0 || input.Lines.Any(l => l.Amount < 0 || string.IsNullOrWhiteSpace(l.Name)))
            return BadRequest(new { message = "Every line needs a name and an amount of zero or more." });

        // Keep the claim link only for claims already on this slip, so a claim cannot be paid twice.
        var claimIds = slip.Lines.Where(l => l.RequestId != null).Select(l => l.RequestId).ToHashSet();
        db.PayslipLines.RemoveRange(slip.Lines);
        slip.Lines = input.Lines.Select(l => new PayslipLine
        {
            Kind = l.Kind,
            Name = l.Name.Trim(),
            Amount = Math.Round(l.Amount, 2),
            Statutory = l.Statutory,
            Category = l.Category,
            Source = claimIds.Contains(l.RequestId) ? l.Source : l.Source == PayLineSource.Claim ? PayLineSource.Manual : l.Source,
            RequestId = claimIds.Contains(l.RequestId) ? l.RequestId : null,
        }).ToList();

        slip.BasicSalary = Math.Round(input.BasicSalary, 2);
        slip.Pcb = Math.Round(input.Pcb, 2);
        slip.StatutoryOverride = input.StatutoryOverride;
        if (input.StatutoryOverride)
        {
            slip.EpfEmployee = input.EpfEmployee;
            slip.EpfEmployer = input.EpfEmployer;
            slip.SocsoEmployee = input.SocsoEmployee;
            slip.SocsoEmployer = input.SocsoEmployer;
            slip.EisEmployee = input.EisEmployee;
            slip.EisEmployer = input.EisEmployer;
            slip.SkbbkEmployee = input.SkbbkEmployee;
        }
        PayrollService.Calculate(slip, await settings.LoadAsync(), slip.PayrollRun.Year, slip.PayrollRun.Month);
        audit.Log("Update", nameof(Payslip), id, slip.EmployeeName);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Simple bank upload file: name, account, amount. Most Malaysian banks accept a CSV like this or can map it.</summary>
    [HttpGet("runs/{id:int}/bank.csv")]
    public async Task<IActionResult> BankFile(int id)
    {
        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return NotFound();
        var csv = new StringBuilder("Employee No,Name,IC Number,Bank,Account No,Amount\n");
        foreach (var p in run.Payslips.OrderBy(p => p.EmployeeNo))
            csv.AppendJoin(',', Csv(p.EmployeeNo), Csv(p.EmployeeName), Csv(p.IcNumber), Csv(p.BankName), Csv(p.BankAccountNo),
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
        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
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
        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return NotFound();
        var employerCode = await db.Settings.Where(s => s.Key == "EmployerSocsoCode").Select(s => s.Value).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(employerCode))
            return BadRequest(new { message = "Enter the PERKESO employer code under Setup → Company first." });
        var regNo = await db.Settings.Where(s => s.Key == "CompanyRegNo").Select(s => s.Value).FirstOrDefaultAsync();
        var text = StatutoryFiles.Perkeso(run, employerCode.Trim(), regNo?.Trim());
        audit.Log("Export", nameof(PayrollRun), id, "PERKESO file");
        await db.SaveChangesAsync();
        // ASSIST rejects a file name it has seen before, so stamp the time.
        return File(Encoding.ASCII.GetBytes(text), "text/plain", $"PERKESO-{run.Year}{run.Month:D2}-{DateTime.Now:yyyyMMddHHmm}.txt");
    }

    [HttpGet("runs/{id:int}/export.xlsx")]
    public async Task<IActionResult> Export(int id)
    {
        var run = await db.PayrollRuns.Include(r => r.Payslips).FirstOrDefaultAsync(r => r.Id == id);
        if (run is null) return NotFound();

        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet($"Payroll {run.Year}-{run.Month:D2}");
        string[] headers = ["Employee No", "Name", "IC Number", "Department", "Basic", "Gross", "EPF (Employee)", "EPF (Employer)",
            "SOCSO (Employee)", "SOCSO (Employer)", "EIS (Employee)", "EIS (Employer)", "SKBBK (Employee)", "PCB", "Total Deductions", "Net Pay",
            "EPF No", "SOCSO No", "Tax No", "Bank", "Account No"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var p in run.Payslips.OrderBy(p => p.EmployeeNo))
        {
            object?[] values = [p.EmployeeNo, p.EmployeeName, p.IcNumber, p.Department, p.BasicSalary, p.GrossPay, p.EpfEmployee, p.EpfEmployer,
                p.SocsoEmployee, p.SocsoEmployer, p.EisEmployee, p.EisEmployer, p.SkbbkEmployee, p.Pcb, p.TotalDeductions, p.NetPay,
                p.EpfNo, p.SocsoNo, p.TaxNo, p.BankName, p.BankAccountNo];
            for (var i = 0; i < values.Length; i++)
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
    public async Task<IActionResult> GetSettings() => Ok(await settings.LoadRawAsync());

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
        audit.Log("Update", nameof(Setting), null, "Payroll rates");
        await db.SaveChangesAsync();
        return NoContent();
    }

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
        var p = await db.Payslips.Include(x => x.Lines).Include(x => x.PayrollRun).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        var isOwner = p.UserId == User.UserId() && p.PayrollRun!.Status == PayrollStatus.Finalized;
        if (!isOwner && !User.IsAdminOrHR()) return NotFound();

        var company = await db.Settings.Where(s => s.Key == "CompanyName").Select(s => s.Value).FirstOrDefaultAsync();
        return Ok(new
        {
            p.Id, p.PayrollRunId, p.PayrollRun!.Year, p.PayrollRun.Month, RunStatus = p.PayrollRun.Status, CompanyName = company,
            p.UserId, p.EmployeeNo, p.EmployeeName, p.Department, p.Position, p.IcNumber, p.EpfNo, p.SocsoNo, p.TaxNo, p.BankName, p.BankAccountNo,
            p.BasicSalary, p.GrossPay, p.StatutoryWage, p.StatutoryOverride,
            p.PassportNo, p.EpfCategory, p.SocsoCategory, p.EisEnabled, p.SkbbkEnabled,
            p.EpfEmployee, p.EpfEmployer, p.SocsoEmployee, p.SocsoEmployer, p.EisEmployee, p.EisEmployer, p.SkbbkEmployee, p.Pcb,
            p.TotalDeductions, p.NetPay,
            Lines = p.Lines.OrderBy(l => l.Kind).ThenBy(l => l.Id).Select(l => new { l.Kind, l.Name, l.Amount, l.Statutory, l.Category, l.Source, l.RequestId }),
        });
    }
}
