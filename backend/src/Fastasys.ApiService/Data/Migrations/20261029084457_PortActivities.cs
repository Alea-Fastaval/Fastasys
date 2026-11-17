using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <summary>
    /// Reshapes the scaffold activities table to the Infosys aktiviteter fields (+ activityageranges as MinAge/MaxAge)
    /// and stops deleting an activity from cascading to its schedules.
    /// Defaults below only apply to rows that already exist; they match the entity defaults.
    /// </summary>
    public partial class PortActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_schedule_activities_ActivityId",
                table: "schedule");

            migrationBuilder.RenameColumn(
                name: "MinParticipants",
                table: "activities",
                newName: "MinPlayersPerTeam");

            migrationBuilder.RenameColumn(
                name: "MaxParticipants",
                table: "activities",
                newName: "MaxPlayersPerTeam");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "activities");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "activities");

            migrationBuilder.AlterColumn<string>(
                name: "TitleEnglish",
                table: "activities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "activities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Author",
                table: "activities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "BoardGame");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEnglish",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeaserDanish",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TeaserEnglish",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DurationHours",
                table: "activities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "GameMastersPerTeam",
                table: "activities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Price",
                table: "activities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxSignups",
                table: "activities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinAge",
                table: "activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAge",
                table: "activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanSignUp",
                table: "activities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "activities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRoomExclusive",
                table: "activities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTimeExclusive",
                table: "activities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReplayable",
                table: "activities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlayerLanguage",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "Danish");

            migrationBuilder.AddColumn<string>(
                name: "GameMasterLanguage",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "Danish");

            migrationBuilder.AddColumn<int>(
                name: "KarmaType",
                table: "activities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AutoSignupCategory",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "WordPressId",
                table: "activities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "activities",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddForeignKey(
                name: "FK_schedule_activities_ActivityId",
                table: "schedule",
                column: "ActivityId",
                principalTable: "activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_schedule_activities_ActivityId",
                table: "schedule");

            migrationBuilder.DropColumn(name: "Type", table: "activities");
            migrationBuilder.DropColumn(name: "DescriptionEnglish", table: "activities");
            migrationBuilder.DropColumn(name: "TeaserDanish", table: "activities");
            migrationBuilder.DropColumn(name: "TeaserEnglish", table: "activities");
            migrationBuilder.DropColumn(name: "Note", table: "activities");
            migrationBuilder.DropColumn(name: "DurationHours", table: "activities");
            migrationBuilder.DropColumn(name: "GameMastersPerTeam", table: "activities");
            migrationBuilder.DropColumn(name: "Price", table: "activities");
            migrationBuilder.DropColumn(name: "MaxSignups", table: "activities");
            migrationBuilder.DropColumn(name: "MinAge", table: "activities");
            migrationBuilder.DropColumn(name: "MaxAge", table: "activities");
            migrationBuilder.DropColumn(name: "CanSignUp", table: "activities");
            migrationBuilder.DropColumn(name: "IsHidden", table: "activities");
            migrationBuilder.DropColumn(name: "IsRoomExclusive", table: "activities");
            migrationBuilder.DropColumn(name: "IsTimeExclusive", table: "activities");
            migrationBuilder.DropColumn(name: "IsReplayable", table: "activities");
            migrationBuilder.DropColumn(name: "PlayerLanguage", table: "activities");
            migrationBuilder.DropColumn(name: "GameMasterLanguage", table: "activities");
            migrationBuilder.DropColumn(name: "KarmaType", table: "activities");
            migrationBuilder.DropColumn(name: "AutoSignupCategory", table: "activities");
            migrationBuilder.DropColumn(name: "WordPressId", table: "activities");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "activities");

            migrationBuilder.AlterColumn<string>(
                name: "TitleEnglish",
                table: "activities",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "activities",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Author",
                table: "activities",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "activities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "activities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "activities",
                type: "integer",
                nullable: false,
                defaultValue: 240);

            migrationBuilder.RenameColumn(
                name: "MaxPlayersPerTeam",
                table: "activities",
                newName: "MaxParticipants");

            migrationBuilder.RenameColumn(
                name: "MinPlayersPerTeam",
                table: "activities",
                newName: "MinParticipants");

            migrationBuilder.AddForeignKey(
                name: "FK_schedule_activities_ActivityId",
                table: "schedule",
                column: "ActivityId",
                principalTable: "activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
