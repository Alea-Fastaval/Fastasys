using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class PortWear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipantWears");

            migrationBuilder.DropTable(
                name: "WearItems");

            migrationBuilder.CreateTable(
                name: "wear_attributes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'20', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    NameDanish = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wear_attributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wear_images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wear_images", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wear_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameDanish = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NameEnglish = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DescriptionDanish = table.Column<string>(type: "text", nullable: false),
                    DescriptionEnglish = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    MaxOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wear_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wear_image_links",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WearImageId = table.Column<int>(type: "integer", nullable: false),
                    WearTypeId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wear_image_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wear_image_links_wear_attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "wear_attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wear_image_links_wear_images_WearImageId",
                        column: x => x.WearImageId,
                        principalTable: "wear_images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wear_image_links_wear_types_WearTypeId",
                        column: x => x.WearTypeId,
                        principalTable: "wear_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wear_prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WearTypeId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantCategoryId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wear_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wear_prices_participant_categories_ParticipantCategoryId",
                        column: x => x.ParticipantCategoryId,
                        principalTable: "participant_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wear_prices_wear_types_WearTypeId",
                        column: x => x.WearTypeId,
                        principalTable: "wear_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wear_variant_attributes",
                columns: table => new
                {
                    WearTypeId = table.Column<int>(type: "integer", nullable: false),
                    Variant = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wear_variant_attributes", x => new { x.WearTypeId, x.Variant, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_wear_variant_attributes_wear_attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "wear_attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wear_variant_attributes_wear_types_WearTypeId",
                        column: x => x.WearTypeId,
                        principalTable: "wear_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "participant_wear_orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    WearPriceId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    IsReceived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_wear_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_participant_wear_orders_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_wear_orders_wear_prices_WearPriceId",
                        column: x => x.WearPriceId,
                        principalTable: "wear_prices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participant_wear_order_attributes",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    AttributeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participant_wear_order_attributes", x => new { x.OrderId, x.AttributeId });
                    table.ForeignKey(
                        name: "FK_participant_wear_order_attributes_participant_wear_orders_O~",
                        column: x => x.OrderId,
                        principalTable: "participant_wear_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_participant_wear_order_attributes_wear_attributes_Attribute~",
                        column: x => x.AttributeId,
                        principalTable: "wear_attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "wear_attributes",
                columns: new[] { "Id", "Kind", "NameDanish", "NameEnglish", "Position" },
                values: new object[,]
                {
                    { 1, "Size", "Baby 56(0-1 md)", "Baby 56(0-1 mo)", 1 },
                    { 2, "Size", "Baby 62(2-3 md)", "Baby 62(2-3 mo)", 2 },
                    { 3, "Size", "Baby 68(3-6 md)", "Baby 68(3-6 mo)", 3 },
                    { 4, "Size", "Baby 74(6-9 md)", "Baby 74(6-9 mo)", 4 },
                    { 5, "Size", "Baby 80(9-12 md)", "Baby 80(9-12 mo)", 5 },
                    { 6, "Size", "Baby 86/92(1-2 år)", "Baby 86/92(1-2 yr)", 6 },
                    { 7, "Size", "Barn 98/104(3-4 år)", "Child 98/104(3-4 yr)", 7 },
                    { 8, "Size", "Barn 110/116(5-6 år)", "Child 110/116(5-6 yr)", 8 },
                    { 9, "Size", "Barn 122/128(7-8 år)", "Child 122/128(7-8 yr)", 9 },
                    { 10, "Size", "Barn 134/146(9-11 år)", "Child 134/146(9-11 yr)", 10 },
                    { 11, "Size", "Barn 152/164(12-14 år)", "Child 152/164(12-14 yr)", 11 },
                    { 12, "Size", "S", "S", 12 },
                    { 13, "Size", "M", "M", 13 },
                    { 14, "Size", "L", "L", 14 },
                    { 15, "Size", "XL", "XL", 15 },
                    { 16, "Size", "XXL", "XXL", 16 },
                    { 17, "Size", "3XL", "3XL", 17 },
                    { 18, "Size", "4XL", "4XL", 18 },
                    { 19, "Size", "5XL", "5XL", 19 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_participant_wear_order_attributes_AttributeId",
                table: "participant_wear_order_attributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_wear_orders_ParticipantId",
                table: "participant_wear_orders",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_participant_wear_orders_WearPriceId",
                table: "participant_wear_orders",
                column: "WearPriceId");

            migrationBuilder.CreateIndex(
                name: "IX_wear_image_links_AttributeId",
                table: "wear_image_links",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_wear_image_links_WearImageId_WearTypeId_AttributeId",
                table: "wear_image_links",
                columns: new[] { "WearImageId", "WearTypeId", "AttributeId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_wear_image_links_WearTypeId",
                table: "wear_image_links",
                column: "WearTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_wear_images_FileName",
                table: "wear_images",
                column: "FileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wear_prices_ParticipantCategoryId",
                table: "wear_prices",
                column: "ParticipantCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_wear_prices_WearTypeId_ParticipantCategoryId",
                table: "wear_prices",
                columns: new[] { "WearTypeId", "ParticipantCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wear_variant_attributes_AttributeId",
                table: "wear_variant_attributes",
                column: "AttributeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participant_wear_order_attributes");

            migrationBuilder.DropTable(
                name: "wear_image_links");

            migrationBuilder.DropTable(
                name: "wear_variant_attributes");

            migrationBuilder.DropTable(
                name: "participant_wear_orders");

            migrationBuilder.DropTable(
                name: "wear_images");

            migrationBuilder.DropTable(
                name: "wear_attributes");

            migrationBuilder.DropTable(
                name: "wear_prices");

            migrationBuilder.DropTable(
                name: "wear_types");

            migrationBuilder.CreateTable(
                name: "WearItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Size = table.Column<string>(type: "text", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WearItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantWears",
                columns: table => new
                {
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    WearItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantWears", x => new { x.ParticipantId, x.WearItemId });
                    table.ForeignKey(
                        name: "FK_ParticipantWears_WearItems_WearItemId",
                        column: x => x.WearItemId,
                        principalTable: "WearItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantWears_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantWears_WearItemId",
                table: "ParticipantWears",
                column: "WearItemId");
        }
    }
}
