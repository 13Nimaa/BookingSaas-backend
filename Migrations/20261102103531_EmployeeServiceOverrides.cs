using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingSaas_backend.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeServiceOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutesOverride",
                table: "EmployeeServices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceOverride",
                table: "EmployeeServices",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutesOverride",
                table: "EmployeeServices");

            migrationBuilder.DropColumn(
                name: "PriceOverride",
                table: "EmployeeServices");
        }
    }
}
