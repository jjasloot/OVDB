using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class stationMaps2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoutesMaps_StationMaps_StationMapMapId",
                table: "RoutesMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_StationMapCountries_StationMaps_StationMapId",
                table: "StationMapCountries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StationMaps",
                table: "StationMaps");

            migrationBuilder.DropIndex(
                name: "IX_RoutesMaps_StationMapMapId",
                table: "RoutesMaps");

            migrationBuilder.DropColumn(
                name: "MapId",
                table: "StationMaps");

            migrationBuilder.DropColumn(
                name: "Default",
                table: "StationMaps");

            migrationBuilder.DropColumn(
                name: "ShowRouteInfo",
                table: "StationMaps");

            migrationBuilder.DropColumn(
                name: "ShowRouteOutline",
                table: "StationMaps");

            migrationBuilder.DropColumn(
                name: "StationMapMapId",
                table: "RoutesMaps");

            migrationBuilder.AddColumn<int>(
                name: "StationMapId",
                table: "StationMaps",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StationMaps",
                table: "StationMaps",
                column: "StationMapId");

            migrationBuilder.AddForeignKey(
                name: "FK_StationMapCountries_StationMaps_StationMapId",
                table: "StationMapCountries",
                column: "StationMapId",
                principalTable: "StationMaps",
                principalColumn: "StationMapId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StationMapCountries_StationMaps_StationMapId",
                table: "StationMapCountries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StationMaps",
                table: "StationMaps");

            migrationBuilder.DropColumn(
                name: "StationMapId",
                table: "StationMaps");

            migrationBuilder.AddColumn<int>(
                name: "MapId",
                table: "StationMaps",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<bool>(
                name: "Default",
                table: "StationMaps",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowRouteInfo",
                table: "StationMaps",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowRouteOutline",
                table: "StationMaps",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StationMapMapId",
                table: "RoutesMaps",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StationMaps",
                table: "StationMaps",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutesMaps_StationMapMapId",
                table: "RoutesMaps",
                column: "StationMapMapId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoutesMaps_StationMaps_StationMapMapId",
                table: "RoutesMaps",
                column: "StationMapMapId",
                principalTable: "StationMaps",
                principalColumn: "MapId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StationMapCountries_StationMaps_StationMapId",
                table: "StationMapCountries",
                column: "StationMapId",
                principalTable: "StationMaps",
                principalColumn: "MapId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
