using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Managey.Api.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class BandFixedAmounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Rounding",
                table: "ContributionSchemes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeFixed",
                table: "ContributionBands",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerFixed",
                table: "ContributionBands",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeFixed",
                table: "ContributionBands");

            migrationBuilder.DropColumn(
                name: "EmployerFixed",
                table: "ContributionBands");

            migrationBuilder.AlterColumn<string>(
                name: "Rounding",
                table: "ContributionSchemes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);
        }
    }
}
