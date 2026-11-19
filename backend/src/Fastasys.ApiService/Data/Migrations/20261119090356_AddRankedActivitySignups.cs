using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRankedActivitySignups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_participant_schedules_schedule_ScheduleId",
                table: "participant_schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_participant_schedules",
                table: "participant_schedules");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "participant_schedules",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "participant_schedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_participant_schedules",
                table: "participant_schedules",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_participant_schedules_ParticipantId_ScheduleId_Priority",
                table: "participant_schedules",
                columns: new[] { "ParticipantId", "ScheduleId", "Priority" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "FK_participant_schedules_schedule_ScheduleId",
                table: "participant_schedules",
                column: "ScheduleId",
                principalTable: "schedule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old key cannot represent combined GM/player preferences. Never choose a row to discard.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM participant_schedules
                        GROUP BY "ParticipantId", "ScheduleId" HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot roll back ranked signups while a participant has multiple preferences for one schedule';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_participant_schedules_schedule_ScheduleId",
                table: "participant_schedules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_participant_schedules",
                table: "participant_schedules");

            migrationBuilder.DropIndex(
                name: "IX_participant_schedules_ParticipantId_ScheduleId_Priority",
                table: "participant_schedules");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "participant_schedules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "participant_schedules");

            migrationBuilder.AddPrimaryKey(
                name: "PK_participant_schedules",
                table: "participant_schedules",
                columns: new[] { "ParticipantId", "ScheduleId" });

            migrationBuilder.AddForeignKey(
                name: "FK_participant_schedules_schedule_ScheduleId",
                table: "participant_schedules",
                column: "ScheduleId",
                principalTable: "schedule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
