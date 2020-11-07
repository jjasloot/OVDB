using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class stationlistings2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OsmId",
                table: "Stations",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsmId",
                table: "Stations");
        }
    }
}
