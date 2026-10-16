using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Managey.Api.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class StatutoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EisEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "EpfCategory",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "PassportNo",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SkbbkEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SocsoCategory",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "First");

            migrationBuilder.AddColumn<bool>(
                name: "EisEnabled",
                table: "Payslips",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "EpfCategory",
                table: "Payslips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "PassportNo",
                table: "Payslips",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SkbbkEmployee",
                table: "Payslips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SkbbkEnabled",
                table: "Payslips",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SocsoCategory",
                table: "Payslips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "First");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "PayslipLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Allowance");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "EmployeePayItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Allowance");
            // Existing pay lines: classify for the EA form.
            migrationBuilder.Sql("UPDATE PayslipLines SET Category = 'Reimbursement' WHERE Source = 'Claim'");
            migrationBuilder.Sql("UPDATE PayslipLines SET Category = 'Salary' WHERE Source = 'UnpaidLeave'");
            migrationBuilder.Sql("UPDATE PayslipLines SET Category = 'Other' WHERE Kind = 'Deduction' AND Source <> 'UnpaidLeave'");
            migrationBuilder.Sql("UPDATE EmployeePayItems SET Category = 'Other' WHERE Kind = 'Deduction'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EisEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EpfCategory",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PassportNo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SkbbkEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SocsoCategory",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EisEnabled",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "EpfCategory",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "PassportNo",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SkbbkEmployee",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SkbbkEnabled",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "SocsoCategory",
                table: "Payslips");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PayslipLines");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "EmployeePayItems");
        }
    }
}
