using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class Adddistancetoroute : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropForeignKey(
            //    name: "FK_Routes_Maps_MapId",
            //    table: "Routes");

            //migrationBuilder.DropIndex(
            //    name: "IX_Routes_MapId",
            //    table: "Routes");

            //migrationBuilder.DropColumn(
            //    name: "MapId",
            //    table: "Routes");

            migrationBuilder.AddColumn<double>(
                name: "CalculatedDistance",
                table: "Routes",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OverrideDistance",
                table: "Routes",
                nullable: true,
                defaultValue: 0.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalculatedDistance",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "OverrideDistance",
                table: "Routes");

            migrationBuilder.AddColumn<int>(
                name: "MapId",
                table: "Routes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_MapId",
                table: "Routes",
                column: "MapId");

            migrationBuilder.AddForeignKey(
                name: "FK_Routes_Maps_MapId",
                table: "Routes",
                column: "MapId",
                principalTable: "Maps",
                principalColumn: "MapId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
