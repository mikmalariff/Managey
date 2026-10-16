using System.Globalization;
using System.Text;
using Managey.Api.Data;

namespace Managey.Api.Services;

/// <summary>Monthly upload files for KWSP (i-Akaun e-Caruman) and PERKESO (ASSIST).</summary>
public static class StatutoryFiles
{
    /// <summary>
    /// KWSP e-Caruman CSV, one employee per row, no header:
    /// member no, IC no (######-##-####), name, wages (2 decimals), employer share, employee share (whole ringgit).
    /// </summary>
    public static string Kwsp(IEnumerable<Payslip> payslips)
    {
        var csv = new StringBuilder();
        foreach (var p in payslips.Where(p => p.EpfEmployee + p.EpfEmployer > 0).OrderBy(p => p.EmployeeNo))
        {
            csv.AppendJoin(',',
                Clean(p.EpfNo, 20),
                FormatIc(p.IcNumber) ?? Clean(p.PassportNo, 20),
                Clean(p.EmployeeName, 80).ToUpperInvariant(),
                p.StatutoryWage.ToString("0.00", CultureInfo.InvariantCulture),
                p.EpfEmployer.ToString("0", CultureInfo.InvariantCulture),
                p.EpfEmployee.ToString("0", CultureInfo.InvariantCulture));
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
        foreach (var p in run.Payslips
                     .Where(p => p.SocsoEmployer + p.SocsoEmployee + p.EisEmployer + p.EisEmployee + p.SkbbkEmployee > 0)
                     .OrderBy(p => p.EmployeeNo))
        {
            var id = Digits(p.IcNumber) is { Length: 12 } ic ? ic : Clean(p.SocsoNo, 12);
            var line = new StringBuilder()
                .Append(Left(employerCode, 12))
                .Append(Left(companyRegNo, 20))
                .Append(Left(id, 12))
                .Append(Left(Clean(p.EmployeeName, 150).ToUpperInvariant(), 150))
                .Append(month)
                .Append(Sen(p.StatutoryWage, 14))
                .Append(Sen(p.SocsoEmployer, 6))
                .Append(Sen(p.SocsoEmployee, 6))
                .Append(Sen(p.EisEmployer, 6))
                .Append(Sen(p.EisEmployee, 6))
                .Append(Sen(p.SkbbkEmployee, 6))
                .Append(new string(' ', 34));
            text.Append(line).Append("\r\n");
        }
        return text.ToString();
    }

    public const int PerkesoLineLength = 278;

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
