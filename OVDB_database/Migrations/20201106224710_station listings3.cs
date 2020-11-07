using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class stationlistings3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OsmId",
                table: "StationCountries",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsmId",
                table: "StationCountries");
        }
    }
}
