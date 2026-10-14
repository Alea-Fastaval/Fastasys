using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class Nicknames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "participants");
        }
    }
}
