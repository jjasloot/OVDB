using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class OrderingandTranslations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameNL",
                table: "RouteTypes",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderNr",
                table: "RouteTypes",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionNL",
                table: "Routes",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameNL",
                table: "Routes",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameNL",
                table: "Maps",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderNr",
                table: "Maps",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NameNL",
                table: "Countries",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderNr",
                table: "Countries",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameNL",
                table: "RouteTypes");

            migrationBuilder.DropColumn(
                name: "OrderNr",
                table: "RouteTypes");

            migrationBuilder.DropColumn(
                name: "DescriptionNL",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "NameNL",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "NameNL",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "OrderNr",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "NameNL",
                table: "Countries");

            migrationBuilder.DropColumn(
                name: "OrderNr",
                table: "Countries");
        }
    }
}
