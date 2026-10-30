using System;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingSaas_backend.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261030000000_AddWorkingHourDate")]
    public partial class AddWorkingHourDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows get a placeholder date; recreate real hours after migrate if any exist.
            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "WorkingHours",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Date",
                table: "WorkingHours");
        }
    }
}
