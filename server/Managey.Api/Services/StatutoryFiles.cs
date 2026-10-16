using System.Globalization;
using System.Text;
using Managey.Api.Data;

namespace Managey.Api.Services;

/// <summary>
/// Monthly contribution files. <see cref="Contributions"/> works for any scheme; the KWSP and PERKESO files are
/// Malaysia-specific and read the scheme codes created by the Malaysia preset.
/// </summary>
public static class StatutoryFiles
{
    public static readonly string[] EpfCodes = ["EPF", "EPF60", "EPFFW"];
    public static readonly string[] SocsoCodes = ["SOCSO", "SOCSO2"];

    /// <summary>One row per employee for a scheme: ID, member no., wages, employee and employer amounts.</summary>
    public static string Contributions(IEnumerable<Payslip> payslips, string code)
    {
        var csv = new StringBuilder("Employee No,Name,ID No,Member No,Wages,Employee,Employer,Total\r\n");
        foreach (var p in payslips.OrderBy(p => p.EmployeeNo))
        {
            var c = p.Contributions.Where(c => c.Code == code).ToList();
            if (c.Count == 0) continue;
            decimal ee = c.Sum(x => x.Employee), er = c.Sum(x => x.Employer);
            csv.AppendJoin(',', Csv(p.EmployeeNo), Csv(p.EmployeeName), Csv(p.IcNumber ?? p.PassportNo), Csv(c[0].MemberNo),
                Money(p.StatutoryWage), Money(ee), Money(er), Money(ee + er)).Append("\r\n");
        }
        return csv.ToString();
    }

    /// <summary>
    /// KWSP e-Caruman CSV, one employee per row, no header:
    /// member no, IC no (######-##-####), name, wages (2 decimals), employer share, employee share (whole ringgit).
    /// </summary>
    public static string Kwsp(IEnumerable<Payslip> payslips)
    {
        var csv = new StringBuilder();
        foreach (var p in payslips.OrderBy(p => p.EmployeeNo))
        {
            var epf = p.Contributions.Where(c => EpfCodes.Contains(c.Code)).ToList();
            if (epf.Sum(c => c.Employee + c.Employer) <= 0) continue;
            csv.AppendJoin(',',
                Clean(epf.Select(c => c.MemberNo).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)), 20),
                FormatIc(p.IcNumber) ?? Clean(p.PassportNo, 20),
                Clean(p.EmployeeName, 80).ToUpperInvariant(),
                p.StatutoryWage.ToString("0.00", CultureInfo.InvariantCulture),
                epf.Sum(c => c.Employer).ToString("0", CultureInfo.InvariantCulture),
                epf.Sum(c => c.Employee).ToString("0", CultureInfo.InvariantCulture));
            csv.Append("\r\n");
        }
        return csv.ToString();
    }

    /// <summary>
    /// PERKESO combined SOCSO + EIS contribution text file (mandatory from 1 October 2026), fixed width, 278 characters a line:
    /// 1–12 employer code, 13–32 MyCoID/SSM no., 33–44 IC (or SSN), 45–194 name, 195–200 month (mmyyyy),
    /// 201–214 wages, 215–220 SOCSO employer, 221–226 SOCSO employee, 227–232 EIS employer, 233–238 EIS employee,
    /// 239–244 SKBBK employee, 245–278 filler. Amounts are in sen without a decimal point, right-aligned and zero-filled.
    /// </summary>
    public static string Perkeso(PayrollRun run, string employerCode, string? companyRegNo = null)
    {
        var text = new StringBuilder();
        var month = $"{run.Month:D2}{run.Year:D4}";
        foreach (var p in run.Payslips.OrderBy(p => p.EmployeeNo))
        {
            var socso = p.Contributions.Where(c => SocsoCodes.Contains(c.Code)).ToList();
            var eis = p.Contributions.Where(c => c.Code == "EIS").ToList();
            var skbbk = p.Contributions.Where(c => c.Code == "SKBBK").ToList();
            if (socso.Concat(eis).Concat(skbbk).Sum(c => c.Employee + c.Employer) <= 0) continue;

            var ssn = socso.Concat(eis).Select(c => c.MemberNo).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
            var id = Digits(p.IcNumber) is { Length: 12 } ic ? ic : Clean(ssn, 12);
            var line = new StringBuilder()
                .Append(Left(employerCode, 12))
                .Append(Left(companyRegNo, 20))
                .Append(Left(id, 12))
                .Append(Left(Clean(p.EmployeeName, 150).ToUpperInvariant(), 150))
                .Append(month)
                .Append(Sen(p.StatutoryWage, 14))
                .Append(Sen(socso.Sum(c => c.Employer), 6))
                .Append(Sen(socso.Sum(c => c.Employee), 6))
                .Append(Sen(eis.Sum(c => c.Employer), 6))
                .Append(Sen(eis.Sum(c => c.Employee), 6))
                .Append(Sen(skbbk.Sum(c => c.Employee), 6))
                .Append(new string(' ', 34));
            text.Append(line).Append("\r\n");
        }
        return text.ToString();
    }

    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Csv(string? s) =>
        s is null ? "" : s.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    private static string Sen(decimal amount, int width) =>
        ((long)Math.Round(amount * 100, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');

    private static string Left(string? value, int width)
    {
        var v = value ?? "";
        return v.Length >= width ? v[..width] : v.PadRight(width);
    }

    /// <summary>Printable ASCII only, no commas (CSV) and no line breaks.</summary>
    private static string Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var chars = value.Trim().Select(c => c is >= ' ' and <= '~' && c != ',' ? c : ' ').ToArray();
        var s = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return s.Length > max ? s[..max] : s;
    }

    private static string? Digits(string? value) =>
        value is null ? null : new string(value.Where(char.IsDigit).ToArray());

    private static string? FormatIc(string? ic) =>
        Digits(ic) is { Length: 12 } d ? $"{d[..6]}-{d[6..8]}-{d[8..]}" : null;
}
