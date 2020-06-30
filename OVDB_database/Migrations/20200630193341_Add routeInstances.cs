using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class AddrouteInstances : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RouteInstances",
                columns: table => new
                {
                    RouteInstanceId = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTime>(nullable: false),
                    RouteId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteInstances", x => x.RouteInstanceId);
                    table.ForeignKey(
                        name: "FK_RouteInstances_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteInstanceProperties",
                columns: table => new
                {
                    RouteInstancePropertyId = table.Column<long>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RouteInstanceId = table.Column<int>(nullable: false),
                    Key = table.Column<string>(nullable: true),
                    Value = table.Column<string>(nullable: true),
                    Date = table.Column<DateTime>(nullable: true),
                    Bool = table.Column<bool>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteInstanceProperties", x => x.RouteInstancePropertyId);
                    table.ForeignKey(
                        name: "FK_RouteInstanceProperties_RouteInstances_RouteInstanceId",
                        column: x => x.RouteInstanceId,
                        principalTable: "RouteInstances",
                        principalColumn: "RouteInstanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstanceProperties_RouteInstanceId",
                table: "RouteInstanceProperties",
                column: "RouteInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstances_RouteId",
                table: "RouteInstances",
                column: "RouteId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteInstanceProperties");

            migrationBuilder.DropTable(
                name: "RouteInstances");
        }
    }
}
