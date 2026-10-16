namespace Managey.Api.Data;

public enum UserRole { Admin, HR, Manager, Staff }

public enum RequestType { Leave, Claim, Purchase, General }

public enum RequestStatus { Pending, Approved, Rejected, Cancelled }

public enum StepStatus { Waiting, Pending, Approved, Rejected, Skipped }

public enum ApproverKind { DirectManager, HR, Admin, SpecificUser }

public enum PayItemKind { Earning, Deduction }

public enum PayrollStatus { Draft, Finalized }

public enum PayLineSource { Manual, Recurring, Claim, UnpaidLeave }

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
    public decimal? MonthlyPcb { get; set; }
    public string? IcNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNo { get; set; }
    public string? EpfNo { get; set; }
    public string? SocsoNo { get; set; }
    public string? TaxNo { get; set; }
}

/// <summary>A fixed allowance or deduction added to the employee's payslip every month.</summary>
public class EmployeePayItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public PayItemKind Kind { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>Whether the amount counts as wages for EPF, SOCSO and EIS.</summary>
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
    public string? EpfNo { get; set; }
    public string? SocsoNo { get; set; }
    public string? TaxNo { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNo { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal GrossPay { get; set; }
    public decimal StatutoryWage { get; set; }
    /// <summary>When true, EPF/SOCSO/EIS were typed in by HR and are not recalculated.</summary>
    public bool StatutoryOverride { get; set; }
    public decimal EpfEmployee { get; set; }
    public decimal EpfEmployer { get; set; }
    public decimal SocsoEmployee { get; set; }
    public decimal SocsoEmployer { get; set; }
    public decimal EisEmployee { get; set; }
    public decimal EisEmployer { get; set; }
    public decimal Pcb { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public List<PayslipLine> Lines { get; set; } = new();
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
