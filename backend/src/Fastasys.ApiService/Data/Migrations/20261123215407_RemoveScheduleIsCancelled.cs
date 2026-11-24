using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveScheduleIsCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CancelledAt becomes the only cancellation state. Every query used IsCancelled, so a row whose two columns
            // disagree keeps the state the flag gave it; a cancellation without a time gets the migration's time.
            migrationBuilder.Sql(
                """
                UPDATE schedule SET "CancelledAt" = now() WHERE "IsCancelled" AND "CancelledAt" IS NULL;
                UPDATE schedule SET "CancelledAt" = NULL WHERE NOT "IsCancelled" AND "CancelledAt" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "schedule");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "schedule",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE schedule SET "IsCancelled" = true WHERE "CancelledAt" IS NOT NULL;
                """);
        }
    }
}
