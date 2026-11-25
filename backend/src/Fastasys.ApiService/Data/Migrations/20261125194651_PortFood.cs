using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    // Written by hand: EF read the new food_types as a rename of the placeholder FoodTypes (IsActive becoming IsHidden,
    // and an empty unique NameDanish for the old rows). As approved on 2026-11-25, the placeholder tables FoodTypes and
    // ParticipantFoods, which only held demo rows, are dropped and the food tables are created.
    public partial class PortFood : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ParticipantFoods");

            migrationBuilder.DropTable(name: "FoodTypes");

            migrationBuilder.CreateTable(
                name: "food_types",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    NameDanish = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_food_types", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "meal_times",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    FoodTypeId = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DescriptionDanish = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    DescriptionEnglish = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_times", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meal_times_food_types_FoodTypeId",
                        column: x => x.FoodTypeId,
                        principalTable: "food_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "participant_meals",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    MealTimeId = table.Column<int>(type: "integer", nullable: false),
                    IsReceived = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_meals", x => new { x.ParticipantId, x.MealTimeId });
                    table.ForeignKey(
                        name: "FK_participant_meals_meal_times_MealTimeId",
                        column: x => x.MealTimeId,
                        principalTable: "meal_times",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_participant_meals_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_food_types_NameDanish",
                table: "food_types",
                column: "NameDanish",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_meal_times_FoodTypeId_Time",
                table: "meal_times",
                columns: new[] { "FoodTypeId", "Time" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_participant_meals_MealTimeId",
                table: "participant_meals",
                column: "MealTimeId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "participant_meals");

            migrationBuilder.DropTable(name: "meal_times");

            migrationBuilder.DropTable(name: "food_types");

            migrationBuilder.CreateTable(
                name: "FoodTypes",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NameEnglish = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodTypes", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ParticipantFoods",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    FoodTypeId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantFoods", x => new
                    {
                        x.ParticipantId,
                        x.FoodTypeId,
                        x.Date,
                    });
                    table.ForeignKey(
                        name: "FK_ParticipantFoods_FoodTypes_FoodTypeId",
                        column: x => x.FoodTypeId,
                        principalTable: "FoodTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ParticipantFoods_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantFoods_FoodTypeId",
                table: "ParticipantFoods",
                column: "FoodTypeId"
            );
        }
    }
}
