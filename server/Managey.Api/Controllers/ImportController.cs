using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Managey.Api.Data;
using Managey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Controllers;

/// <summary>
/// Bulk create or update employees from an Excel (.xlsx) or CSV file. Rows are matched on Employee No.
/// A preview is always run first; nothing is saved unless every row is valid.
/// </summary>
[ApiController]
[Route("api/users/import")]
[Authorize(Roles = Roles.AdminOrHR)]
public class ImportController(AppDbContext db, IPasswordHasher<User> hasher, AuditService audit) : ControllerBase
{
    private static readonly (string Header, string Example, string Note)[] BaseColumns =
    [
        ("Employee No", "E0101", "Required. Existing numbers are updated, new ones are created."),
        ("Full Name", "Aisyah Binti Kamal", "Required."),
        ("Username", "aisyah", "Optional. Defaults to the part of the email before @, or the employee no."),
        ("Email", "aisyah@company.my", "Optional. Needed for email notifications."),
        ("Phone", "012-3456789", ""),
        ("Department", "Finance", "Created automatically if it does not exist."),
        ("Position", "Account Executive", ""),
        ("Role", "Staff", "Staff, Manager or HR (Admin only if you are an admin). Defaults to Staff."),
        ("Reports To", "E0002", "Employee No of their manager. Can be someone in this file."),
        ("Join Date", "2024-03-01", "yyyy-mm-dd or dd/mm/yyyy."),
        ("Basic Salary", "3500", "Monthly, in your currency."),
        ("ID Number", "900101-14-5678", "National ID (e.g. MyKad, NRIC). 'IC Number' is also accepted."),
        ("Passport No", "", "Employees without a national ID."),
        ("Bank Name", "Maybank", ""),
        ("Bank Account No", "512345678901", "Format the column as Text so leading zeros are kept."),
        ("Tax No", "SG12345678090", ""),
        ("Password", "", "Optional, new employees only. Leave blank to generate one."),
    ];

    /// <summary>Other header names people commonly use for a column.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IC Number"] = "ID Number", ["IC No"] = "ID Number", ["NRIC"] = "ID Number", ["MyKad"] = "ID Number",
    };

    /// <summary>Base columns plus "{Code} No" for every scheme and "{Code} Amount" for per-employee schemes.</summary>
    private async Task<(List<(string Header, string Example, string Note)> Columns, List<ContributionScheme> Schemes)> ColumnsAsync()
    {
        var schemes = await db.ContributionSchemes.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();
        var columns = BaseColumns.ToList();
        foreach (var s in schemes)
        {
            columns.Add(($"{s.Code} No", "", $"Member number for {s.Name}."));
            if (s.Method == ContributionMethod.PerEmployee)
                columns.Add(($"{s.Code} Amount", "", $"Monthly employee amount for {s.Name}."));
        }
        return (columns, schemes);
    }

    public record ImportRow(int Row, string? EmployeeNo, string? FullName, string Action, List<string> Messages, string? TempPassword);

    [HttpGet("template")]
    public async Task<IActionResult> Template()
    {
        var (Columns, _) = await ColumnsAsync();
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Employees");
        for (var i = 0; i < Columns.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = Columns[i].Header;
            sheet.Cell(2, i + 1).Value = Columns[i].Example;
            sheet.Column(i + 1).Style.NumberFormat.Format = "@"; // text, keeps leading zeros
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        var notes = book.AddWorksheet("How to fill");
        notes.Cell(1, 1).Value = "Column";
        notes.Cell(1, 2).Value = "Notes";
        notes.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < Columns.Count; i++)
        {
            notes.Cell(i + 2, 1).Value = Columns[i].Header;
            notes.Cell(i + 2, 2).Value = Columns[i].Note;
        }
        notes.Cell(Columns.Count + 3, 1).Value = "Delete the example row before importing.";
        notes.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "managey-employees-template.xlsx");
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import([FromForm] IFormFile file, [FromQuery] bool commit = false)
    {
        var (columns, schemes) = await ColumnsAsync();
        var known = columns.Select(c => c.Header).ToList();
        List<Dictionary<string, string>> records;
        try
        {
            records = Path.GetExtension(file.FileName).ToLowerInvariant() switch
            {
                ".xlsx" => ReadXlsx(file, known),
                ".csv" => ReadCsv(file, known),
                _ => throw new FormatException("Upload an .xlsx or .csv file."),
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or IOException)
        {
            return BadRequest(new { message = ex.Message });
        }
        if (records.Count == 0) return BadRequest(new { message = "The file has no employee rows." });
        if (records.Count > 2000) return BadRequest(new { message = "Import at most 2,000 employees at a time." });

        var existing = await db.Users.ToListAsync();
        var byEmpNo = existing.ToDictionary(u => u.EmployeeNo, StringComparer.OrdinalIgnoreCase);
        var departments = await db.Departments.ToListAsync();
        var fileEmpNos = records.Select(r => Get(r, "Employee No")).Where(x => x is not null).ToList();

        var results = new List<ImportRow>();
        var plans = new List<(Dictionary<string, string> Record, User User, bool IsNew, string? Password, string? ManagerNo, string? Department,
            List<(ContributionScheme Scheme, string? MemberNo, decimal? Amount)> Memberships)>();
        var usernamesTaken = new HashSet<string>(existing.Select(u => u.Username), StringComparer.OrdinalIgnoreCase);
        var seenEmpNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var rowNo = i + 2; // header is row 1
            var messages = new List<string>();
            var empNo = Get(r, "Employee No");
            var name = Get(r, "Full Name");

            if (empNo is null) messages.Add("Employee No is required.");
            else if (!seenEmpNos.Add(empNo)) messages.Add("Employee No appears more than once in the file.");
            if (name is null) messages.Add("Full Name is required.");

            var isNew = empNo is null || !byEmpNo.ContainsKey(empNo);
            var user = !isNew ? byEmpNo[empNo!] : new User { EmployeeNo = empNo ?? "" };
            if (!isNew)
                usernamesTaken.Remove(user.Username); // the row may keep or change its own username

            var email = Get(r, "Email");
            if (email is not null && !email.Contains('@')) messages.Add("Email does not look valid.");

            var username = Get(r, "Username") ?? (isNew ? (email?.Split('@')[0] ?? empNo)?.ToLowerInvariant() : user.Username);
            if (username is not null && !usernamesTaken.Add(username)) messages.Add($"Username '{username}' is already used.");

            UserRole? role = null;
            if (Get(r, "Role") is { } roleText)
            {
                if (!Enum.TryParse<UserRole>(roleText, true, out var parsed)) messages.Add($"Unknown role '{roleText}'.");
                else if (parsed == UserRole.Admin && !User.IsInRole(Roles.Admin)) messages.Add("Only an admin can import admins.");
                else role = parsed;
            }
            if (!isNew && user.Role == UserRole.Admin && !User.IsInRole(Roles.Admin))
                messages.Add("Only an admin can update an admin account.");

            var managerNo = Get(r, "Reports To");
            if (managerNo is not null)
            {
                if (string.Equals(managerNo, empNo, StringComparison.OrdinalIgnoreCase)) messages.Add("An employee cannot report to themselves.");
                else if (!byEmpNo.ContainsKey(managerNo) && !fileEmpNos.Contains(managerNo, StringComparer.OrdinalIgnoreCase))
                    messages.Add($"Reports To '{managerNo}' is not an existing employee or in this file.");
            }

            var joinDate = ParseDate(Get(r, "Join Date"), messages);
            var basic = ParseMoney(Get(r, "Basic Salary"), "Basic Salary", messages);
            var memberships = new List<(ContributionScheme, string?, decimal?)>();
            foreach (var scheme in schemes)
            {
                var memberNo = Get(r, $"{scheme.Code} No");
                var amount = scheme.Method == ContributionMethod.PerEmployee
                    ? ParseMoney(Get(r, $"{scheme.Code} Amount"), $"{scheme.Code} Amount", messages) : null;
                if (memberNo is not null || amount is not null) memberships.Add((scheme, memberNo, amount));
            }
            var password = Get(r, "Password");
            if (password is not null && isNew && UsersController.PasswordProblem(password) is { } problem) messages.Add(problem);

            if (messages.Count > 0)
            {
                results.Add(new ImportRow(rowNo, empNo, name, "Error", messages, null));
                continue;
            }

            user.FullName = name!;
            user.Username = username!;
            if (email is not null) user.Email = email;
            if (Get(r, "Phone") is { } phone) user.Phone = phone;
            if (Get(r, "Position") is { } position) user.Position = position;
            if (role is not null) user.Role = role.Value;
            if (joinDate is not null) user.JoinDate = joinDate;
            UsersController.ApplyPay(user, basic ?? user.BasicSalary,
                Get(r, "ID Number") ?? user.IcNumber, Get(r, "Passport No") ?? user.PassportNo, Get(r, "Tax No") ?? user.TaxNo,
                Get(r, "Bank Name") ?? user.BankName, Get(r, "Bank Account No") ?? user.BankAccountNo);

            var tempPassword = isNew && password is null ? GeneratePassword() : null;
            plans.Add((r, user, isNew, password ?? tempPassword, managerNo, Get(r, "Department"), memberships));
            results.Add(new ImportRow(rowNo, empNo, name, isNew ? "Create" : "Update", [], commit ? tempPassword : null));
        }

        var errors = results.Count(r => r.Action == "Error");
        if (!commit || errors > 0)
        {
            // Never keep the edits made to tracked users during validation.
            db.ChangeTracker.Clear();
            return Ok(new { committed = false, created = results.Count(r => r.Action == "Create"), updated = results.Count(r => r.Action == "Update"), errors, rows = results });
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var plan in plans)
        {
            if (plan.Department is { } deptName)
            {
                var dept = departments.FirstOrDefault(d => string.Equals(d.Name, deptName, StringComparison.OrdinalIgnoreCase));
                if (dept is null)
                {
                    dept = new Department { Name = deptName };
                    departments.Add(dept);
                    db.Departments.Add(dept);
                }
                plan.User.Department = dept;
            }
            if (plan.IsNew)
            {
                plan.User.PasswordHash = hasher.HashPassword(plan.User, plan.Password!);
                db.Users.Add(plan.User);
                byEmpNo[plan.User.EmployeeNo] = plan.User;
            }
        }
        await db.SaveChangesAsync();

        foreach (var plan in plans.Where(p => p.ManagerNo is not null))
            plan.User.ManagerId = byEmpNo[plan.ManagerNo!].Id;

        await UsersController.EnrolInDefaultSchemesAsync(db, plans.Where(p => p.IsNew).Select(p => p.User.Id).ToList());
        var planUserIds = plans.Select(p => p.User.Id).ToList();
        var current = await db.EmployeeContributions.Where(c => planUserIds.Contains(c.UserId)).ToListAsync();
        foreach (var plan in plans)
            foreach (var (scheme, memberNo, amount) in plan.Memberships)
            {
                var row = current.FirstOrDefault(c => c.UserId == plan.User.Id && c.SchemeId == scheme.Id);
                if (row is null)
                {
                    row = new EmployeeContribution { UserId = plan.User.Id, SchemeId = scheme.Id };
                    db.EmployeeContributions.Add(row);
                }
                if (memberNo is not null) row.MemberNo = memberNo;
                if (amount is not null) row.EmployeeAmount = amount;
            }

        audit.Log("Import", nameof(User), null, $"{results.Count(r => r.Action == "Create")} created, {results.Count(r => r.Action == "Update")} updated from {file.FileName}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { committed = true, created = results.Count(r => r.Action == "Create"), updated = results.Count(r => r.Action == "Update"), errors = 0, rows = results });
    }

    private static string? Get(Dictionary<string, string> r, string column) =>
        r.TryGetValue(column, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static DateTime? ParseDate(string? text, List<string> messages)
    {
        if (text is null) return null;
        string[] formats = ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "yyyy/MM/dd", "d MMM yyyy"];
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d.Date;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 1 and < 80000)
            return DateTime.FromOADate(serial).Date; // Excel date serial
        messages.Add($"Join Date '{text}' is not a date (use yyyy-mm-dd).");
        return null;
    }

    private static decimal? ParseMoney(string? text, string column, List<string> messages)
    {
        if (text is null) return null;
        // Drop thousands separators and currency symbols/codes (RM, $, S$...).
        var cleaned = System.Text.RegularExpressions.Regex.Replace(text.Replace(",", ""), @"[^0-9.\-]", "");
        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v >= 0) return v;
        messages.Add($"{column} '{text}' is not a valid amount.");
        return null;
    }

    private static string GeneratePassword()
    {
        const string chars = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 10).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
    }

    private static Dictionary<string, string> MapHeaders(IReadOnlyList<string> headers, IReadOnlyList<string> values, List<string> known)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count && i < values.Count; i++)
        {
            var name = Aliases.TryGetValue(headers[i].Trim(), out var alias) ? alias : headers[i];
            var header = known.FirstOrDefault(k => string.Equals(Normalize(k), Normalize(name), StringComparison.OrdinalIgnoreCase));
            if (header is not null) row[header] = values[i];
        }
        return row;

        static string Normalize(string s) => new(s.Where(char.IsLetterOrDigit).ToArray());
    }

    private static void RequireHeaders(IReadOnlyList<string> headers, List<string> known)
    {
        var mapped = MapHeaders(headers, headers, known);
        if (!mapped.ContainsKey("Employee No") || !mapped.ContainsKey("Full Name"))
            throw new FormatException("The first row must contain the column headers, including 'Employee No' and 'Full Name'. Download the template to see the layout.");
    }

    private static List<Dictionary<string, string>> ReadXlsx(IFormFile file, List<string> known)
    {
        using var stream = file.OpenReadStream();
        using var book = new XLWorkbook(stream);
        var sheet = book.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null) return [];

        var lastCol = used.LastColumn().ColumnNumber();
        var headers = Enumerable.Range(1, lastCol).Select(c => sheet.Cell(1, c).GetString()).ToList();
        RequireHeaders(headers, known);

        var rows = new List<Dictionary<string, string>>();
        for (var r = 2; r <= used.LastRow().RowNumber(); r++)
        {
            var values = Enumerable.Range(1, lastCol).Select(c =>
            {
                var cell = sheet.Cell(r, c);
                return cell.DataType == XLDataType.DateTime ? cell.GetDateTime().ToString("yyyy-MM-dd") : cell.GetFormattedString();
            }).ToList();
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(MapHeaders(headers, values, known));
        }
        return rows;
    }

    private static List<Dictionary<string, string>> ReadCsv(IFormFile file, List<string> known)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var lines = ParseCsv(reader.ReadToEnd());
        if (lines.Count == 0) return [];
        RequireHeaders(lines[0], known);
        return lines.Skip(1).Where(l => l.Any(v => !string.IsNullOrWhiteSpace(v))).Select(l => MapHeaders(lines[0], l, known)).ToList();
    }

    /// <summary>RFC 4180 CSV: quoted fields, escaped quotes, newlines inside quotes.</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
