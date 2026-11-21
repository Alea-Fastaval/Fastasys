using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fastasys.ApiService.Data.Migrations
{
    /// <summary>
    /// Data only, no schema change. The ID card text sources "Dr. + Navn", "Scenarie" and "Ungdomsskole" were dropped
    /// on 2026-11-21, so template text items that still use one of them are deleted: they would no longer load.
    /// </summary>
    public partial class RemoveIdCardDataSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """DELETE FROM id_card_template_items WHERE "DataSource" IN ('DoctorName', 'Game', 'YouthClub');""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The deleted items can't be brought back
        }
    }
}
