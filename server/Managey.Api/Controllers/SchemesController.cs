using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

/// <summary>Contribution schemes (pension, social security, tax...) that make up statutory deductions in any country.</summary>
[ApiController]
[Route("api/payroll")]
[Authorize(Roles = Roles.AdminOrHR)]
public class SchemesController(AppDbContext db, AuditService audit) : ControllerBase
{
    public record BandInput(decimal UpTo, decimal Employee, decimal Employer, decimal Step);

    public record SchemeInput(
        string Name, string Code, ContributionMethod Method, decimal EmployeeRate, decimal EmployerRate,
        decimal? WageCeiling, ContributionRounding Rounding, DateTime? EffectiveFrom, bool DefaultForNewEmployees, bool MemberNoRequired,
        string? EmployerRef, int SortOrder, bool IsActive, List<BandInput> Bands);

    [HttpGet("schemes")]
    public async Task<IActionResult> List()
    {
        var members = await db.EmployeeContributions.GroupBy(c => c.SchemeId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var schemes = await db.ContributionSchemes.Include(s => s.Bands).OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync();
        return Ok(schemes.Select(s => new
        {
            s.Id, s.Name, s.Code, s.Method, s.EmployeeRate, s.EmployerRate, s.WageCeiling, s.Rounding, s.EffectiveFrom,
            s.DefaultForNewEmployees, s.MemberNoRequired, s.EmployerRef, s.SortOrder, s.IsActive,
            Members = members.GetValueOrDefault(s.Id),
            Bands = s.Bands.OrderBy(b => b.UpTo).Select(b => new { b.UpTo, b.Employee, b.Employer, b.Step }),
        }));
    }

    [HttpPost("schemes")]
    public async Task<IActionResult> Create(SchemeInput input)
    {
        if (await ValidateAsync(input, null) is { } error) return BadRequest(new { message = error });
        var scheme = new ContributionScheme();
        Apply(scheme, input);
        db.ContributionSchemes.Add(scheme);
        await db.SaveChangesAsync();
        audit.Log("Create", nameof(ContributionScheme), scheme.Id, scheme.Name);
        await db.SaveChangesAsync();
        return Ok(new { scheme.Id });
    }

    [HttpPut("schemes/{id:int}")]
    public async Task<IActionResult> Update(int id, SchemeInput input)
    {
        var scheme = await db.ContributionSchemes.Include(s => s.Bands).FirstOrDefaultAsync(s => s.Id == id);
        if (scheme is null) return NotFound();
        if (await ValidateAsync(input, id) is { } error) return BadRequest(new { message = error });
        db.ContributionBands.RemoveRange(scheme.Bands);
        Apply(scheme, input);
        audit.Log("Update", nameof(ContributionScheme), id, scheme.Name);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("schemes/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var scheme = await db.ContributionSchemes.FindAsync(id);
        if (scheme is null) return NotFound();
        // Existing payslips keep their copied amounts; only memberships go.
        db.ContributionSchemes.Remove(scheme);
        audit.Log("Delete", nameof(ContributionScheme), id, scheme.Name);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("presets")]
    public IActionResult Presets() => Ok(PayrollPresets.All);

    /// <summary>Adds a country's schemes (skipping codes that already exist), sets the currency, and enrols current staff in the default schemes.</summary>
    [HttpPost("presets/{key}")]
    public async Task<IActionResult> ApplyPreset(string key)
    {
        var preset = PayrollPresets.All.FirstOrDefault(p => p.Key == key);
        if (preset is null) return NotFound();

        var existing = await db.ContributionSchemes.Select(s => s.Code).ToListAsync();
        var added = PayrollPresets.Build(key).Where(s => !existing.Contains(s.Code)).ToList();
        db.ContributionSchemes.AddRange(added);

        var currency = await db.Settings.FindAsync("Currency");
        if (currency is null) db.Settings.Add(new Setting { Key = "Currency", Value = preset.Currency });
        else currency.Value = preset.Currency;
        await db.SaveChangesAsync();

        var staff = await db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
        foreach (var scheme in added.Where(s => s.DefaultForNewEmployees))
            db.EmployeeContributions.AddRange(staff.Select(u => new EmployeeContribution { UserId = u, SchemeId = scheme.Id }));

        audit.Log("ApplyPreset", nameof(ContributionScheme), null, $"{preset.Name}: {added.Count} scheme(s) added");
        await db.SaveChangesAsync();
        return Ok(new { added = added.Count });
    }

    private async Task<string?> ValidateAsync(SchemeInput input, int? id)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Code)) return "Name and code are required.";
        if (input.Code.Trim().Length > 20) return "Code can be at most 20 characters.";
        if (await db.ContributionSchemes.AnyAsync(s => s.Id != id && s.Code == input.Code.Trim())) return "Another scheme already uses that code.";
        if (input.EmployeeRate < 0 || input.EmployerRate < 0 || input.WageCeiling < 0) return "Rates and amounts cannot be negative.";
        if (input.Method is ContributionMethod.RateBands or ContributionMethod.AmountBands)
        {
            if (input.Bands.Count == 0) return "Add at least one wage band.";
            if (input.Bands.Any(b => b.UpTo <= 0 || b.Employee < 0 || b.Employer < 0 || b.Step < 0)) return "Band values must be zero or more, and every band needs an upper limit.";
            if (input.Bands.Select(b => b.UpTo).Distinct().Count() != input.Bands.Count) return "Two bands have the same upper limit.";
        }
        return null;
    }

    private static void Apply(ContributionScheme s, SchemeInput input)
    {
        s.Name = input.Name.Trim();
        s.Code = input.Code.Trim().ToUpperInvariant();
        s.Method = input.Method;
        s.EmployeeRate = input.EmployeeRate;
        s.EmployerRate = input.EmployerRate;
        s.WageCeiling = input.WageCeiling is > 0 ? input.WageCeiling : null;
        s.Rounding = input.Rounding;
        s.EffectiveFrom = input.EffectiveFrom?.Date;
        s.DefaultForNewEmployees = input.DefaultForNewEmployees;
        s.MemberNoRequired = input.MemberNoRequired;
        s.EmployerRef = string.IsNullOrWhiteSpace(input.EmployerRef) ? null : input.EmployerRef.Trim();
        s.SortOrder = input.SortOrder;
        s.IsActive = input.IsActive;
        s.Bands = input.Method is ContributionMethod.RateBands or ContributionMethod.AmountBands
            ? input.Bands.OrderBy(b => b.UpTo).Select(b => new ContributionBand { UpTo = b.UpTo, Employee = b.Employee, Employer = b.Employer, Step = b.Step }).ToList()
            : [];
    }
}
