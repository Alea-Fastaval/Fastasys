using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTicketTypeIsDigital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDigital",
                table: "ticket_types");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDigital",
                table: "ticket_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
