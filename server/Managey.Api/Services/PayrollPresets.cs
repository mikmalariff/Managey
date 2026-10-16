using Managey.Api.Data;

namespace Managey.Api.Services;

/// <summary>Ready-made contribution schemes for a country. Companies can edit them afterwards like any scheme.</summary>
public static class PayrollPresets
{
    public record Preset(string Key, string Name, string Currency, string Description);

    public static readonly Preset[] All =
    [
        new("MY", "Malaysia", "MYR",
            "EPF (standard, 60+ and foreign worker rates), SOCSO categories 1 and 2, EIS, LINDUNG 24 Jam (SKBBK) and monthly tax (PCB/MTD), using the official contribution tables."),
    ];

    public static List<ContributionScheme> Build(string key) => key switch
    {
        "MY" => Malaysia(),
        _ => throw new ArgumentException($"Unknown preset '{key}'."),
    };

    /// <summary>
    /// EPF uses the Third Schedule approach (wages rounded up to RM20 bands up to RM5,000, RM100 bands up to RM20,000,
    /// result rounded up to the ringgit). SOCSO, EIS and SKBBK use PERKESO's tables (see <see cref="PerkesoTables"/>).
    /// </summary>
    private static List<ContributionScheme> Malaysia()
    {
        static List<ContributionBand> Epf(decimal employee, decimal employerUpTo5000, decimal employerAbove) =>
        [
            new() { UpTo = 10, Employee = 0, Employer = 0 },
            new() { UpTo = 5000, Employee = employee, Employer = employerUpTo5000, Step = 20 },
            new() { UpTo = 20000, Employee = employee, Employer = employerAbove, Step = 100 },
            new() { UpTo = 999_999_999, Employee = employee, Employer = employerAbove },
        ];

        static List<ContributionBand> Perkeso(Func<PerkesoTables.Band, (decimal Employee, decimal Employer)> pick) =>
            PerkesoTables.Bands.Select(b => { var (ee, er) = pick(b); return new ContributionBand { UpTo = b.UpTo, Employee = ee, Employer = er }; }).ToList();

        return
        [
            new() { Code = "EPF", Name = "EPF (KWSP)", Method = ContributionMethod.RateBands, Rounding = ContributionRounding.UpWhole, MemberNoRequired = true,
                DefaultForNewEmployees = true, SortOrder = 10, Bands = Epf(11, 13, 12) },
            new() { Code = "EPF60", Name = "EPF (KWSP), aged 60+", Method = ContributionMethod.RateBands, Rounding = ContributionRounding.UpWhole, MemberNoRequired = true,
                SortOrder = 11, Bands = Epf(0, 4, 4) },
            new() { Code = "EPFFW", Name = "EPF (KWSP), foreign worker", Method = ContributionMethod.RateBands, Rounding = ContributionRounding.UpWhole, MemberNoRequired = true,
                SortOrder = 12, Bands = Epf(2, 2, 2) },
            new() { Code = "SOCSO", Name = "SOCSO (PERKESO)", Method = ContributionMethod.AmountBands,
                DefaultForNewEmployees = true, SortOrder = 20, Bands = Perkeso(b => (b.SocsoEmployee, b.SocsoEmployer)) },
            new() { Code = "SOCSO2", Name = "SOCSO (PERKESO), second category", Method = ContributionMethod.AmountBands,
                SortOrder = 21, Bands = Perkeso(b => (0, b.SocsoEmployerCategory2)) },
            new() { Code = "EIS", Name = "EIS (SIP)", Method = ContributionMethod.AmountBands,
                DefaultForNewEmployees = true, SortOrder = 30, Bands = Perkeso(b => (b.Eis, b.Eis)) },
            new() { Code = "SKBBK", Name = "LINDUNG 24 Jam (SKBBK)", Method = ContributionMethod.AmountBands,
                DefaultForNewEmployees = true, SortOrder = 40, EffectiveFrom = new DateTime(2026, 6, 1), Bands = Perkeso(b => (b.SkbbkEmployee, 0)) },
            new() { Code = "PCB", Name = "Income tax (PCB/MTD)", Method = ContributionMethod.PerEmployee,
                DefaultForNewEmployees = true, SortOrder = 50 },
        ];
    }
}
