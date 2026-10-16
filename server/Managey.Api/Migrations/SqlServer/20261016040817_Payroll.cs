using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Managey.Api.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Payroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountNo",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BasicSalary",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpfNo",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IcNumber",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPcb",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocsoNo",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxNo",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnpaid",
                table: "LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing installs: the seeded "Unpaid" leave type should be deducted from pay.
            migrationBuilder.Sql("UPDATE LeaveTypes SET IsUnpaid = 1 WHERE Name = 'Unpaid'");

            migrationBuilder.CreateTable(
                name: "EmployeePayItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Statutory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Payslips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EmployeeNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IcNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EpfNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SocsoNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BankAccountNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BasicSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StatutoryWage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StatutoryOverride = table.Column<bool>(type: "bit", nullable: false),
                    EpfEmployee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EpfEmployer = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SocsoEmployee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SocsoEmployer = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EisEmployee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EisEmployer = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Pcb = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payslips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payslips_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayslipLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayslipId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Statutory = table.Column<bool>(type: "bit", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayslipLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayslipLines_Payslips_PayslipId",
                        column: x => x.PayslipId,
                        principalTable: "Payslips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayItems_UserId",
                table: "EmployeePayItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_Year_Month",
                table: "PayrollRuns",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayslipLines_PayslipId",
                table: "PayslipLines",
                column: "PayslipId");

            migrationBuilder.CreateIndex(
                name: "IX_PayslipLines_RequestId",
                table: "PayslipLines",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_PayrollRunId_UserId",
                table: "Payslips",
                columns: new[] { "PayrollRunId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payslips_UserId",
                table: "Payslips",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayItems");

            migrationBuilder.DropTable(
                name: "PayslipLines");

            migrationBuilder.DropTable(
                name: "Payslips");

            migrationBuilder.DropTable(
                name: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "BankAccountNo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BasicSalary",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EpfNo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IcNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MonthlyPcb",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SocsoNo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TaxNo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsUnpaid",
                table: "LeaveTypes");
        }
    }
}
