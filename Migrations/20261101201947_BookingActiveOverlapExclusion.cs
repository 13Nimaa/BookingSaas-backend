using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingSaas_backend.Migrations
{
    /// <inheritdoc />
    public partial class BookingActiveOverlapExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // btree_gist is required so gist indexes can include scalar equality
            // (EmployeeId) alongside the range overlap operator.
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS btree_gist;""");

            // Half-open [StartTime, EndTime) via tstzrange bounds '[)'.
            // Partial: only Pending (0) and Confirmed (1) occupy a slot.
            // Raw SQL: EF Core / Npgsql do not model EXCLUDE constraints.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Bookings"
                ADD CONSTRAINT "EX_Bookings_Employee_Active_Time_Overlap"
                EXCLUDE USING gist (
                    "EmployeeId" WITH =,
                    tstzrange("StartTime", "EndTime", '[)') WITH &&
                )
                WHERE ("Status" IN (0, 1));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Bookings"
                DROP CONSTRAINT IF EXISTS "EX_Bookings_Employee_Active_Time_Overlap";
                """);

            // Leave btree_gist installed; other objects may depend on it.
        }
    }
}
