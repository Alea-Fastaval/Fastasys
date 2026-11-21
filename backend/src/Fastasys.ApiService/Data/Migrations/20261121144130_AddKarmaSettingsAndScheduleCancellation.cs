using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKarmaSettingsAndScheduleCancellation : Migration
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

            migrationBuilder.CreateTable(
                name: "activity_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SmallKarma = table.Column<int>(type: "integer", nullable: false),
                    MediumKarma = table.Column<int>(type: "integer", nullable: false),
                    BigKarma = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_settings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "activity_settings",
                columns: new[] { "Id", "BigKarma", "MediumKarma", "SmallKarma" },
                values: new object[] { 1, 10, 0, 7 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back would silently make cancelled schedules active again
            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM schedule WHERE "IsCancelled") THEN
                        RAISE EXCEPTION 'Cannot roll back while cancelled occurrences exist; restore them first';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "activity_settings");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "schedule");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "schedule");
        }
    }
}
