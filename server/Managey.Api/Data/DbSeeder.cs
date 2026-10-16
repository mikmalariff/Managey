using Microsoft.AspNetCore.Identity;
using Managey.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Managey.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, IPasswordHasher<User> hasher)
    {
        if (!await db.Users.AnyAsync())
        {
            var admin = new User
            {
                EmployeeNo = "E0001",
                Username = config["Seed:AdminUsername"] ?? "admin",
                FullName = "Administrator",
                Role = UserRole.Admin,
            };
            admin.PasswordHash = hasher.HashPassword(admin, config["Seed:AdminPassword"] ?? "ChangeMe123!");
            db.Users.Add(admin);
        }

        if (!await db.LeaveTypes.AnyAsync())
        {
            db.LeaveTypes.AddRange(
                new LeaveType { Name = "Annual", DefaultDays = 14 },
                new LeaveType { Name = "Medical", DefaultDays = 14, RequiresAttachment = true },
                new LeaveType { Name = "Emergency", DefaultDays = 3 },
                new LeaveType { Name = "Unpaid", DefaultDays = 30, IsUnpaid = true });
        }

        if (!await db.WorkflowSteps.AnyAsync())
        {
            db.WorkflowSteps.AddRange(
                new WorkflowStep { RequestType = RequestType.Leave, StepOrder = 1, ApproverKind = ApproverKind.DirectManager },
                new WorkflowStep { RequestType = RequestType.Leave, StepOrder = 2, ApproverKind = ApproverKind.HR },
                new WorkflowStep { RequestType = RequestType.Claim, StepOrder = 1, ApproverKind = ApproverKind.DirectManager },
                new WorkflowStep { RequestType = RequestType.Claim, StepOrder = 2, ApproverKind = ApproverKind.HR },
                new WorkflowStep { RequestType = RequestType.Purchase, StepOrder = 1, ApproverKind = ApproverKind.DirectManager },
                new WorkflowStep { RequestType = RequestType.Purchase, StepOrder = 2, ApproverKind = ApproverKind.Admin },
                new WorkflowStep { RequestType = RequestType.General, StepOrder = 1, ApproverKind = ApproverKind.DirectManager });
        }

        var defaults = new (string, string)[]
        {
            ("CompanyName", "My Company"), ("WorkStartTime", "09:00"), ("WorkEndTime", "18:00"),
        }.Concat(PayrollSettings.Defaults.Select(d => (PayrollSettings.Prefix + d.Key, d.Value)));
        foreach (var (key, value) in defaults)
        {
            if (!await db.Settings.AnyAsync(s => s.Key == key))
                db.Settings.Add(new Setting { Key = key, Value = value });
        }

        await db.SaveChangesAsync();
    }
}
