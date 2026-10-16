namespace Managey.Api.Data;

public enum UserRole { Admin, HR, Manager, Staff }

public enum RequestType { Leave, Claim, Purchase, General }

public enum RequestStatus { Pending, Approved, Rejected, Cancelled }

public enum StepStatus { Waiting, Pending, Approved, Rejected, Skipped }

public enum ApproverKind { DirectManager, HR, Admin, SpecificUser }

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
}

public class LeaveType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal DefaultDays { get; set; }
    public bool RequiresAttachment { get; set; }
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
