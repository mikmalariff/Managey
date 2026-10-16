using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Managey.Api.Data;

/// <summary>
/// Shared model. Each database provider has its own subclass so it can keep its own migrations.
/// </summary>
public abstract class AppDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveEntitlement> LeaveEntitlements => Set<LeaveEntitlement>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<EmployeePayItem> EmployeePayItems => Set<EmployeePayItem>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayslipLine> PayslipLines => Set<PayslipLine>();
    public DbSet<PayslipContribution> PayslipContributions => Set<PayslipContribution>();
    public DbSet<ContributionScheme> ContributionSchemes => Set<ContributionScheme>();
    public DbSet<ContributionBand> ContributionBands => Set<ContributionBand>();
    public DbSet<EmployeeContribution> EmployeeContributions => Set<EmployeeContribution>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
        // Timestamps are stored in UTC; providers like SQLite drop DateTimeKind, so restore it on read.
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.EmployeeNo).IsUnique();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasMany(x => x.Contributions).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Department>().HasIndex(x => x.Name).IsUnique();
        b.Entity<LeaveType>().HasIndex(x => x.Name).IsUnique();
        b.Entity<LeaveEntitlement>().HasIndex(x => new { x.UserId, x.LeaveTypeId, x.Year }).IsUnique();

        b.Entity<Request>(e =>
        {
            e.HasIndex(x => x.RefNo).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.NoAction);
            e.HasMany(x => x.Steps).WithOne(x => x.Request!).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Attachments).WithOne(x => x.Request).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApprovalStep>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ApproverKind).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.ApproverUser).WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.ActedBy).WithMany().HasForeignKey(x => x.ActedById).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<WorkflowStep>(e =>
        {
            e.Property(x => x.RequestType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ApproverKind).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Date });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Document>().HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.NoAction);

        b.Entity<AuditLog>().HasIndex(x => x.Timestamp);
        b.Entity<Setting>().HasKey(x => x.Key);
        b.Entity<Holiday>().HasIndex(x => x.Date).IsUnique();

        b.Entity<EmployeePayItem>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.UserId);
        });
        b.Entity<PayrollRun>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Year, x.Month }).IsUnique();
            e.HasMany(x => x.Payslips).WithOne(x => x.PayrollRun!).HasForeignKey(x => x.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Payslip>(e =>
        {
            e.HasIndex(x => new { x.PayrollRunId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PayslipId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Contributions).WithOne().HasForeignKey(x => x.PayslipId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PayslipLine>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.RequestId);
        });
        b.Entity<ContributionScheme>(e =>
        {
            e.Property(x => x.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Rounding).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.EmployeeRate).HasPrecision(9, 4);
            e.Property(x => x.EmployerRate).HasPrecision(9, 4);
            e.HasMany(x => x.Bands).WithOne().HasForeignKey(x => x.SchemeId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ContributionBand>(e =>
        {
            e.Property(x => x.Employee).HasPrecision(18, 4);
            e.Property(x => x.Employer).HasPrecision(18, 4);
        });
        b.Entity<EmployeeContribution>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.SchemeId }).IsUnique();
            e.HasOne(x => x.Scheme).WithMany().HasForeignKey(x => x.SchemeId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
