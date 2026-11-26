using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hero_force_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'8', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameDanish = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hero_force_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participant_hero_availability",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Period = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_hero_availability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participant_hero_availability_hero_force_categories_Categor~",
                        column: x => x.CategoryId,
                        principalTable: "hero_force_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_participant_hero_availability_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "hero_force_categories",
                columns: new[] { "Id", "NameDanish", "NameEnglish" },
                values: new object[,]
                {
                    { 1, "Forplejning", "Food-handling" },
                    { 2, "Service", "Service" },
                    { 3, "Rengøring", "Cleaning" },
                    { 4, "Manuelt arbejde", "Manual labor" },
                    { 5, "Brandvagt", "Night watch" },
                    { 6, "James (Otto-fest tjener)", "James (Otto-celebration waiter)" },
                    { 7, "Ungdomslounge", "Youth lounge" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_participant_hero_availability_CategoryId",
                table: "participant_hero_availability",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_hero_availability_ParticipantId_Date_Period_Cat~",
                table: "participant_hero_availability",
                columns: new[] { "ParticipantId", "Date", "Period", "CategoryId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_hero_availability");

            migrationBuilder.DropTable(
                name: "hero_force_categories");
        }
    }
}
