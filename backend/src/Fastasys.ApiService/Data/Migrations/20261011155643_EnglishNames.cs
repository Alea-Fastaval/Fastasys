using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnglishNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_afvikling_Rooms_RoomId",
                table: "afvikling");

            migrationBuilder.DropForeignKey(
                name: "FK_afvikling_aktiviteter_ActivityId",
                table: "afvikling");

            migrationBuilder.DropForeignKey(
                name: "FK_BoardgameLoans_deltagere_ParticipantId",
                table: "BoardgameLoans");

            migrationBuilder.DropForeignKey(
                name: "FK_participant_schedules_afvikling_ScheduleId",
                table: "participant_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_participant_schedules_deltagere_ParticipantId",
                table: "participant_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ParticipantFoods_deltagere_ParticipantId",
                table: "ParticipantFoods");

            migrationBuilder.DropForeignKey(
                name: "FK_ParticipantWears_deltagere_ParticipantId",
                table: "ParticipantWears");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_deltagere_ParticipantId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_deltagere_ParticipantId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_SignupSubmissions_deltagere_ParticipantId",
                table: "SignupSubmissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_deltagere",
                table: "deltagere");

            migrationBuilder.DropPrimaryKey(
                name: "PK_aktiviteter",
                table: "aktiviteter");

            migrationBuilder.DropPrimaryKey(
                name: "PK_afvikling",
                table: "afvikling");

            migrationBuilder.RenameTable(
                name: "deltagere",
                newName: "participants");

            migrationBuilder.RenameTable(
                name: "aktiviteter",
                newName: "activities");

            migrationBuilder.RenameTable(
                name: "afvikling",
                newName: "schedule");

            migrationBuilder.RenameIndex(
                name: "IX_afvikling_RoomId",
                table: "schedule",
                newName: "IX_schedule_RoomId");

            migrationBuilder.RenameIndex(
                name: "IX_afvikling_ActivityId",
                table: "schedule",
                newName: "IX_schedule_ActivityId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_participants",
                table: "participants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_activities",
                table: "activities",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_schedule",
                table: "schedule",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BoardgameLoans_participants_ParticipantId",
                table: "BoardgameLoans",
                column: "ParticipantId",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_participant_schedules_participants_ParticipantId",
                table: "participant_schedules",
                column: "ParticipantId",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_participant_schedules_schedule_ScheduleId",
                table: "participant_schedules",
                column: "ScheduleId",
                principalTable: "schedule",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParticipantFoods_participants_ParticipantId",
                table: "ParticipantFoods",
                column: "ParticipantId",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParticipantWears_participants_ParticipantId",
                table: "ParticipantWears",
                column: "ParticipantId",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_participants_ParticipantId",
                table: "Payments",
                column: "ParticipantId",
                principalTable: "participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_participants_ParticipantId",
                table: "Sales",
                column: "ParticipantId",
                principalTable: "participants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_schedule_Rooms_RoomId",
                table: "schedule",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_schedule_activities_ActivityId",
                table: "schedule",
                column: "ActivityId",
                principalTable: "activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SignupSubmissions_participants_ParticipantId",
                table: "SignupSubmissions",
                column: "ParticipantId",
                principalTable: "participants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BoardgameLoans_participants_ParticipantId",
                table: "BoardgameLoans");

            migrationBuilder.DropForeignKey(
                name: "FK_participant_schedules_participants_ParticipantId",
                table: "participant_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_participant_schedules_schedule_ScheduleId",
                table: "participant_schedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ParticipantFoods_participants_ParticipantId",
                table: "ParticipantFoods");

            migrationBuilder.DropForeignKey(
                name: "FK_ParticipantWears_participants_ParticipantId",
                table: "ParticipantWears");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_participants_ParticipantId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_participants_ParticipantId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_schedule_Rooms_RoomId",
                table: "schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_schedule_activities_ActivityId",
                table: "schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_SignupSubmissions_participants_ParticipantId",
                table: "SignupSubmissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_schedule",
                table: "schedule");

            migrationBuilder.DropPrimaryKey(
                name: "PK_participants",
                table: "participants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_activities",
                table: "activities");

            migrationBuilder.RenameTable(
                name: "schedule",
                newName: "afvikling");

            migrationBuilder.RenameTable(
                name: "participants",
                newName: "deltagere");

            migrationBuilder.RenameTable(
                name: "activities",
                newName: "aktiviteter");

            migrationBuilder.RenameIndex(
                name: "IX_schedule_RoomId",
                table: "afvikling",
                newName: "IX_afvikling_RoomId");

            migrationBuilder.RenameIndex(
                name: "IX_schedule_ActivityId",
                table: "afvikling",
                newName: "IX_afvikling_ActivityId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_afvikling",
                table: "afvikling",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_deltagere",
                table: "deltagere",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_aktiviteter",
                table: "aktiviteter",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_afvikling_Rooms_RoomId",
                table: "afvikling",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_afvikling_aktiviteter_ActivityId",
                table: "afvikling",
                column: "ActivityId",
                principalTable: "aktiviteter",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoardgameLoans_deltagere_ParticipantId",
                table: "BoardgameLoans",
                column: "ParticipantId",
                principalTable: "deltagere",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_participant_schedules_afvikling_ScheduleId",
                table: "participant_schedules",
                column: "ScheduleId",
                principalTable: "afvikling",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_participant_schedules_deltagere_ParticipantId",
                table: "participant_schedules",
                column: "ParticipantId",
                principalTable: "deltagere",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParticipantFoods_deltagere_ParticipantId",
                table: "ParticipantFoods",
                column: "ParticipantId",
                principalTable: "deltagere",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParticipantWears_deltagere_ParticipantId",
                table: "ParticipantWears",
                column: "ParticipantId",
                principalTable: "deltagere",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_deltagere_ParticipantId",
                table: "Payments",
                column: "ParticipantId",
                principalTable: "deltagere",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_deltagere_ParticipantId",
                table: "Sales",
                column: "ParticipantId",
                principalTable: "deltagere",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SignupSubmissions_deltagere_ParticipantId",
                table: "SignupSubmissions",
                column: "ParticipantId",
                principalTable: "deltagere",
                principalColumn: "Id");
        }
    }
}
