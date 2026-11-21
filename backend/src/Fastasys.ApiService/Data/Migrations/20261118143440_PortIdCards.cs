using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortIdCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdCardTemplateId",
                table: "participants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCardTemplateId",
                table: "participant_categories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "id_card_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Background = table.Column<byte[]>(type: "bytea", nullable: true),
                    BackgroundContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_id_card_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participant_photos",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    Original = table.Column<byte[]>(type: "bytea", nullable: false),
                    OriginalContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Cropped = table.Column<byte[]>(type: "bytea", nullable: false),
                    CroppedContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_photos", x => x.ParticipantId);
                    table.ForeignKey(
                        name: "FK_participant_photos_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "id_card_template_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Rotation = table.Column<int>(type: "integer", nullable: false),
                    DataSource = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_id_card_template_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_id_card_template_items_id_card_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "id_card_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "participant_categories",
                keyColumn: "Id",
                keyValue: 1,
                column: "IdCardTemplateId",
                value: null);

            migrationBuilder.UpdateData(
                table: "participant_categories",
                keyColumn: "Id",
                keyValue: 2,
                column: "IdCardTemplateId",
                value: null);

            migrationBuilder.UpdateData(
                table: "participant_categories",
                keyColumn: "Id",
                keyValue: 3,
                column: "IdCardTemplateId",
                value: null);

            migrationBuilder.UpdateData(
                table: "participant_categories",
                keyColumn: "Id",
                keyValue: 4,
                column: "IdCardTemplateId",
                value: null);

            migrationBuilder.UpdateData(
                table: "participant_categories",
                keyColumn: "Id",
                keyValue: 5,
                column: "IdCardTemplateId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_participants_IdCardTemplateId",
                table: "participants",
                column: "IdCardTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_categories_IdCardTemplateId",
                table: "participant_categories",
                column: "IdCardTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_id_card_template_items_TemplateId",
                table: "id_card_template_items",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_participant_categories_id_card_templates_IdCardTemplateId",
                table: "participant_categories",
                column: "IdCardTemplateId",
                principalTable: "id_card_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_participants_id_card_templates_IdCardTemplateId",
                table: "participants",
                column: "IdCardTemplateId",
                principalTable: "id_card_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_participant_categories_id_card_templates_IdCardTemplateId",
                table: "participant_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_participants_id_card_templates_IdCardTemplateId",
                table: "participants");

            migrationBuilder.DropTable(
                name: "id_card_template_items");

            migrationBuilder.DropTable(
                name: "participant_photos");

            migrationBuilder.DropTable(
                name: "id_card_templates");

            migrationBuilder.DropIndex(
                name: "IX_participants_IdCardTemplateId",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_participant_categories_IdCardTemplateId",
                table: "participant_categories");

            migrationBuilder.DropColumn(
                name: "IdCardTemplateId",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "IdCardTemplateId",
                table: "participant_categories");
        }
    }
}
