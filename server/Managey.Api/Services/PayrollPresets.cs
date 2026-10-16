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
        new("SG", "Singapore", "SGD",
            "CPF for every age band (2026 rates, Ordinary Wage ceiling S$8,000, graduated rates below S$750), Skills Development Levy, and the CDAC, ECF, MBMF and SINDA self-help group funds."),
        new("ID", "Indonesia", "IDR",
            "BPJS Ketenagakerjaan (JHT, JP with the March 2026 wage cap, JKK at the lowest risk rate, JKM), BPJS Kesehatan (Rp12 million cap) and monthly PPh 21."),
    ];

    public static List<ContributionScheme> Build(string key) => key switch
    {
        "MY" => Malaysia(),
        "SG" => Singapore(),
        "ID" => Indonesia(),
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

    /// <summary>
    /// CPF Board rates from 1 January 2026 for Singapore Citizens and SPRs from the third year. Above S$750 the
    /// age band's rates apply up to the S$8,000 Ordinary Wage ceiling. Above S$50 to S$500 only the employer pays;
    /// above S$500 to S$750 the employee share is phased in as (3 × rate) × (wage − 500). The total is rounded to
    /// the nearest dollar and the employee share down to the dollar. Additional Wage (bonus) ceilings are not applied.
    /// </summary>
    private static List<ContributionScheme> Singapore()
    {
        static List<ContributionBand> Cpf(decimal employee, decimal employer) =>
        [
            new() { UpTo = 50, Employee = 0, Employer = 0 },
            new() { UpTo = 500, Employee = 0, Employer = employer },
            new() { UpTo = 750, Employee = 3 * employee, EmployeeFixed = -15 * employee, Employer = employer },
            new() { UpTo = 999_999_999, Employee = employee, Employer = employer },
        ];

        static ContributionScheme CpfScheme(string code, string name, decimal employee, decimal employer, int order, bool isDefault = false) => new()
        {
            Code = code, Name = name, Method = ContributionMethod.RateBands, Rounding = ContributionRounding.TotalNearestEmployeeDown,
            WageCeiling = 8000, MemberNoRequired = false, DefaultForNewEmployees = isDefault, SortOrder = order, Bands = Cpf(employee, employer),
        };

        static ContributionScheme Shg(string code, string name, int order, params (decimal UpTo, decimal Amount)[] rows) => new()
        {
            Code = code, Name = name, Method = ContributionMethod.AmountBands, SortOrder = order,
            Bands = rows.Select(r => new ContributionBand { UpTo = r.UpTo, Employee = r.Amount }).ToList(),
        };

        const decimal Max = 999_999_999;
        return
        [
            CpfScheme("CPF", "CPF, age 55 and below", 20, 17, 10, isDefault: true),
            CpfScheme("CPF60", "CPF, age above 55 to 60", 18, 16, 11),
            CpfScheme("CPF65", "CPF, age above 60 to 65", 12.5m, 12.5m, 12),
            CpfScheme("CPF70", "CPF, age above 65 to 70", 7.5m, 9, 13),
            CpfScheme("CPF70P", "CPF, age above 70", 5, 7.5m, 14),
            // 0.25% of monthly remuneration, at least S$2 (wages under S$800) and at most S$11.25 (wages above S$4,500).
            new() { Code = "SDL", Name = "Skills Development Levy", Method = ContributionMethod.RateBands, DefaultForNewEmployees = true, SortOrder = 20,
                Bands =
                [
                    new() { UpTo = 800, EmployerFixed = 2 },
                    new() { UpTo = 4500, Employer = 0.25m },
                    new() { UpTo = Max, EmployerFixed = 11.25m },
                ] },
            Shg("CDAC", "CDAC Fund", 30, (2000, 0.50m), (3500, 1.00m), (5000, 1.50m), (7500, 2.00m), (Max, 3.00m)),
            Shg("ECF", "Eurasian Community Fund", 31, (1000, 2), (1500, 4), (2500, 6), (4000, 9), (7000, 12), (10000, 16), (Max, 20)),
            Shg("MBMF", "Mosque Building and Mendaki Fund", 32, (1000, 3.00m), (2000, 4.50m), (3000, 6.50m), (4000, 15), (6000, 19.50m), (8000, 22), (10000, 24), (Max, 26)),
            Shg("SINDA", "SINDA Fund", 33, (1000, 1), (1500, 3), (2500, 5), (4500, 7), (7500, 9), (10000, 12), (15000, 18), (Max, 30)),
        ];
    }

    /// <summary>
    /// BPJS rates for 2026. JP's wage cap is revised every March (Rp11,086,300 from 1 March 2026). JKK depends on the
    /// workplace risk class (0.24% to 1.74%); the preset uses the lowest class. PPh 21 is set per employee.
    /// </summary>
    private static List<ContributionScheme> Indonesia() =>
    [
        new() { Code = "JHT", Name = "BPJS TK - Jaminan Hari Tua", Method = ContributionMethod.Percentage, EmployeeRate = 2, EmployerRate = 3.7m,
            Rounding = ContributionRounding.NearestWhole, DefaultForNewEmployees = true, MemberNoRequired = true, SortOrder = 10 },
        new() { Code = "JP", Name = "BPJS TK - Jaminan Pensiun", Method = ContributionMethod.Percentage, EmployeeRate = 1, EmployerRate = 2,
            WageCeiling = 11_086_300, Rounding = ContributionRounding.NearestWhole, DefaultForNewEmployees = true, MemberNoRequired = true, SortOrder = 11 },
        new() { Code = "JKK", Name = "BPJS TK - Jaminan Kecelakaan Kerja", Method = ContributionMethod.Percentage, EmployeeRate = 0, EmployerRate = 0.24m,
            Rounding = ContributionRounding.NearestWhole, DefaultForNewEmployees = true, SortOrder = 12 },
        new() { Code = "JKM", Name = "BPJS TK - Jaminan Kematian", Method = ContributionMethod.Percentage, EmployeeRate = 0, EmployerRate = 0.3m,
            Rounding = ContributionRounding.NearestWhole, DefaultForNewEmployees = true, SortOrder = 13 },
        new() { Code = "BPJSKES", Name = "BPJS Kesehatan", Method = ContributionMethod.Percentage, EmployeeRate = 1, EmployerRate = 4,
            WageCeiling = 12_000_000, Rounding = ContributionRounding.NearestWhole, DefaultForNewEmployees = true, MemberNoRequired = true, SortOrder = 20 },
        new() { Code = "PPH21", Name = "PPh 21", Method = ContributionMethod.PerEmployee, DefaultForNewEmployees = true, SortOrder = 30 },
    ];
}
