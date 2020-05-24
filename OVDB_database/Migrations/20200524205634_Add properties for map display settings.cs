using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class Addpropertiesformapdisplaysettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowRouteInfo",
                table: "Maps",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowRouteOutline",
                table: "Maps",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowRouteInfo",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "ShowRouteOutline",
                table: "Maps");
        }
    }
}
