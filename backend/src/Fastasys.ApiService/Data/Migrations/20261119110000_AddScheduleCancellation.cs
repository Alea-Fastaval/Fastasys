using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "schedule",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "schedule",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the state would reactivate retained preferences/seats without a room check.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM schedule WHERE "IsCancelled") THEN
                        RAISE EXCEPTION 'Cannot roll back cancellation while cancelled occurrences exist; restore or resolve them explicitly first';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "schedule");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "schedule");
        }
    }
}
