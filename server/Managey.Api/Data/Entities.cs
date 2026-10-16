namespace Managey.Api.Data;

public enum UserRole { Admin, HR, Manager, Staff }

public enum RequestType { Leave, Claim, Purchase, General }

public enum RequestStatus { Pending, Approved, Rejected, Cancelled }

public enum StepStatus { Waiting, Pending, Approved, Rejected, Skipped }

public enum ApproverKind { DirectManager, HR, Admin, SpecificUser }

public enum PayItemKind { Earning, Deduction }

public enum PayrollStatus { Draft, Finalized }

public enum PayLineSource { Manual, Recurring, Claim, UnpaidLeave }

/// <summary>How a contribution scheme works out its amounts.</summary>
public enum ContributionMethod
{
    /// <summary>Employee and employer percentages of wages, optionally capped at a wage ceiling.</summary>
    Percentage,
    /// <summary>Percentages that depend on the wage band; each band can round wages up to a step first.</summary>
    RateBands,
    /// <summary>A fixed amount for each wage band (contribution tables).</summary>
    AmountBands,
    /// <summary>The same fixed amount for everyone in the scheme.</summary>
    Fixed,
    /// <summary>An amount set per employee (e.g. monthly income tax from the tax office).</summary>
    PerEmployee,
}

public enum ContributionRounding
{
    Cents,
    NearestWhole,
    UpWhole,
    /// <summary>Total rounded to the nearest whole unit, employee share rounded down, employer pays the rest (Singapore CPF).</summary>
    TotalNearestEmployeeDown,
}

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class User
{
    public int Id { get; set; }
    public string EmployeeNo { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Staff;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Position { get; set; }
    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public DateTime? JoinDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Pay details. Only HR/admin and the employee (through their payslips) may see these.
    public decimal? BasicSalary { get; set; }
    /// <summary>National ID number (e.g. MyKad, NRIC).</summary>
    public string? IcNumber { get; set; }
    public string? PassportNo { get; set; }
    public string? TaxNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNo { get; set; }
    public List<EmployeeContribution> Contributions { get; set; } = new();
}

/// <summary>
/// A statutory or company contribution/deduction such as a pension fund, social security or monthly tax.
/// Defined by the company so the payroll works in any country.
/// </summary>
public class ContributionScheme
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Short label for tables and payslips, e.g. EPF, CPF, SSS. Also used by country-specific export files.</summary>
    public string Code { get; set; } = "";
    public ContributionMethod Method { get; set; }
    public decimal EmployeeRate { get; set; }
    public decimal EmployerRate { get; set; }
    /// <summary>Wages above this are ignored. Null for no ceiling.</summary>
    public decimal? WageCeiling { get; set; }
    public ContributionRounding Rounding { get; set; } = ContributionRounding.Cents;
    /// <summary>First month the scheme applies from (inclusive). Null to always apply.</summary>
    public DateTime? EffectiveFrom { get; set; }
    /// <summary>New employees are added to this scheme automatically.</summary>
    public bool DefaultForNewEmployees { get; set; }
    /// <summary>Warn on payroll when an employee in the scheme has no member number.</summary>
    public bool MemberNoRequired { get; set; }
    /// <summary>The employer's registration number with this scheme, used on export files.</summary>
    public string? EmployerRef { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ContributionBand> Bands { get; set; } = new();
}

/// <summary>
/// One row of a scheme's table: applies to wages above the previous row's limit up to <see cref="UpTo"/>.
/// Wages above the last row use the last row. Values are percentages for RateBands, amounts for AmountBands.
/// </summary>
public class ContributionBand
{
    public int Id { get; set; }
    public int SchemeId { get; set; }
    public decimal UpTo { get; set; }
    public decimal Employee { get; set; }
    public decimal Employer { get; set; }
    /// <summary>RateBands only: round wages up to a multiple of this before applying the rate (0 for none).</summary>
    public decimal Step { get; set; }
    /// <summary>RateBands only: amounts added after the rate (may be negative), for formulas like 0.6 × (wage − 500).</summary>
    public decimal EmployeeFixed { get; set; }
    public decimal EmployerFixed { get; set; }
}

/// <summary>An employee's membership of a scheme.</summary>
public class EmployeeContribution
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int SchemeId { get; set; }
    public ContributionScheme? Scheme { get; set; }
    /// <summary>The employee's member/account number with the scheme.</summary>
    public string? MemberNo { get; set; }
    /// <summary>PerEmployee schemes: the monthly amounts for this person.</summary>
    public decimal? EmployeeAmount { get; set; }
    public decimal? EmployerAmount { get; set; }
}

/// <summary>A fixed allowance or deduction added to the employee's payslip every month.</summary>
public class EmployeePayItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public PayItemKind Kind { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>Whether the amount counts as wages for contribution schemes.</summary>
    public bool Statutory { get; set; } = true;
}

public class PayrollRun
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedById { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public int? FinalizedById { get; set; }
    public List<Payslip> Payslips { get; set; } = new();
}

/// <summary>One employee's pay for one month. Employee details are copied so old slips never change.</summary>
public class Payslip
{
    public int Id { get; set; }
    public int PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public int UserId { get; set; }

    public string EmployeeNo { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string? IcNumber { get; set; }
    public string? PassportNo { get; set; }
    public string? TaxNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNo { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal GrossPay { get; set; }
    /// <summary>Wages that contribution schemes are worked out on.</summary>
    public decimal StatutoryWage { get; set; }
    /// <summary>When true, contributions were typed in by HR and are not recalculated.</summary>
    public bool StatutoryOverride { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal EmployerContributions { get; set; }
    public decimal NetPay { get; set; }

    public List<PayslipLine> Lines { get; set; } = new();
    public List<PayslipContribution> Contributions { get; set; } = new();
}

/// <summary>A scheme's amounts on one payslip. Name and code are copied so old slips never change.</summary>
public class PayslipContribution
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public int? SchemeId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? MemberNo { get; set; }
    public decimal Employee { get; set; }
    public decimal Employer { get; set; }
    public int SortOrder { get; set; }
}

public class PayslipLine
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public PayItemKind Kind { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public bool Statutory { get; set; }
    public PayLineSource Source { get; set; }
    /// <summary>The claim this line reimburses, so a claim is only ever paid once.</summary>
    public int? RequestId { get; set; }
}

public class LeaveType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal DefaultDays { get; set; }
    public bool RequiresAttachment { get; set; }
    /// <summary>Unpaid leave days are deducted from salary in payroll.</summary>
    public bool IsUnpaid { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Per-employee override of a leave type's yearly entitlement.</summary>
public class LeaveEntitlement
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int LeaveTypeId { get; set; }
    public int Year { get; set; }
    public decimal Days { get; set; }
}

/// <summary>A request that goes through an approval workflow (leave, claim, purchase, general).</summary>
public class Request
{
    public int Id { get; set; }
    public string RefNo { get; set; } = "";
    public RequestType Type { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int RequesterId { get; set; }
    public User? Requester { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Leave
    public int? LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool HalfDay { get; set; }
    public decimal? Days { get; set; }

    // Claim / purchase
    public decimal? Amount { get; set; }

    public List<ApprovalStep> Steps { get; set; } = new();
    public List<Document> Attachments { get; set; } = new();
}

public class ApprovalStep
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public Request? Request { get; set; }
    public int StepOrder { get; set; }
    public ApproverKind ApproverKind { get; set; }
    /// <summary>Set when the step belongs to one person (direct manager or a specific user).</summary>
    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }
    public StepStatus Status { get; set; } = StepStatus.Waiting;
    public int? ActedById { get; set; }
    public User? ActedBy { get; set; }
    public DateTime? ActedAt { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Configured approval chain for each request type.</summary>
public class WorkflowStep
{
    public int Id { get; set; }
    public RequestType RequestType { get; set; }
    public int StepOrder { get; set; }
    public ApproverKind ApproverKind { get; set; }
    public int? ApproverUserId { get; set; }
}

public class AttendanceRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    /// <summary>Local (server) calendar date of the work day.</summary>
    public DateTime Date { get; set; }
    public DateTime ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public string? Note { get; set; }
}

/// <summary>A stored file: either a request attachment or a company library document.</summary>
public class Document
{
    public int Id { get; set; }
    public int? RequestId { get; set; }
    public Request? Request { get; set; }
    public string? Category { get; set; }
    public string FileName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long Size { get; set; }
    public int UploadedById { get; set; }
    public User? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    public string Username { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Details { get; set; }
}

public class Setting
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

public class Holiday
{
    public int Id { get; set; }
    /// <summary>Calendar date (no time part).</summary>
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
}
