using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class Addedfromandtotoroute : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "From",
                table: "Routes",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "To",
                table: "Routes",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "From",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "To",
                table: "Routes");
        }
    }
}
