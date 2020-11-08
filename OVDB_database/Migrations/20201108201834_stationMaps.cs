using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class stationMaps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Hidden",
                table: "Stations",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Special",
                table: "Stations",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StationMapMapId",
                table: "RoutesMaps",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StationMaps",
                columns: table => new
                {
                    MapId = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    NameNL = table.Column<string>(nullable: true),
                    MapGuid = table.Column<Guid>(nullable: false),
                    SharingLinkName = table.Column<string>(nullable: true),
                    Default = table.Column<bool>(nullable: false),
                    ShowRouteInfo = table.Column<bool>(nullable: false),
                    ShowRouteOutline = table.Column<bool>(nullable: false),
                    OrderNr = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationMaps", x => x.MapId);
                    table.ForeignKey(
                        name: "FK_StationMaps_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StationMapCountries",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StationMapId = table.Column<int>(nullable: false),
                    StationCountryId = table.Column<int>(nullable: false),
                    IncludeSpecials = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationMapCountries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationMapCountries_StationCountries_StationCountryId",
                        column: x => x.StationCountryId,
                        principalTable: "StationCountries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StationMapCountries_StationMaps_StationMapId",
                        column: x => x.StationMapId,
                        principalTable: "StationMaps",
                        principalColumn: "MapId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoutesMaps_StationMapMapId",
                table: "RoutesMaps",
                column: "StationMapMapId");

            migrationBuilder.CreateIndex(
                name: "IX_StationMapCountries_StationCountryId",
                table: "StationMapCountries",
                column: "StationCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_StationMapCountries_StationMapId",
                table: "StationMapCountries",
                column: "StationMapId");

            migrationBuilder.CreateIndex(
                name: "IX_StationMaps_UserId",
                table: "StationMaps",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoutesMaps_StationMaps_StationMapMapId",
                table: "RoutesMaps",
                column: "StationMapMapId",
                principalTable: "StationMaps",
                principalColumn: "MapId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoutesMaps_StationMaps_StationMapMapId",
                table: "RoutesMaps");

            migrationBuilder.DropTable(
                name: "StationMapCountries");

            migrationBuilder.DropTable(
                name: "StationMaps");

            migrationBuilder.DropIndex(
                name: "IX_RoutesMaps_StationMapMapId",
                table: "RoutesMaps");

            migrationBuilder.DropColumn(
                name: "Hidden",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "Special",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "StationMapMapId",
                table: "RoutesMaps");
        }
    }
}
