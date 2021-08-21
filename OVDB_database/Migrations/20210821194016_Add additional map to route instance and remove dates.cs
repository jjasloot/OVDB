using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class Addadditionalmaptorouteinstanceandremovedates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Date",
                table: "RouteInstanceProperties");

            migrationBuilder.CreateTable(
                name: "RouteInstanceMap",
                columns: table => new
                {
                    RouteMapId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RouteInstanceId = table.Column<int>(type: "int", nullable: false),
                    MapId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteInstanceMap", x => x.RouteMapId);
                    table.ForeignKey(
                        name: "FK_RouteInstanceMap_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "MapId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteInstanceMap_RouteInstances_RouteInstanceId",
                        column: x => x.RouteInstanceId,
                        principalTable: "RouteInstances",
                        principalColumn: "RouteInstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstanceMap_MapId",
                table: "RouteInstanceMap",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstanceMap_RouteInstanceId",
                table: "RouteInstanceMap",
                column: "RouteInstanceId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteInstanceMap");

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "RouteInstanceProperties",
                type: "datetime(6)",
                nullable: true);
        }
    }
}
