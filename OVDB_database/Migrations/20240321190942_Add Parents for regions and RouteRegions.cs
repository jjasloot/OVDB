using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddParentsforregionsandRouteRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentRegionId",
                table: "Regions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RouteRegions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    RegionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteRegions_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteRegions_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_ParentRegionId",
                table: "Regions",
                column: "ParentRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteRegions_RegionId",
                table: "RouteRegions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteRegions_RouteId",
                table: "RouteRegions",
                column: "RouteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Regions_Regions_ParentRegionId",
                table: "Regions",
                column: "ParentRegionId",
                principalTable: "Regions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Regions_Regions_ParentRegionId",
                table: "Regions");

            migrationBuilder.DropTable(
                name: "RouteRegions");

            migrationBuilder.DropIndex(
                name: "IX_Regions_ParentRegionId",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "ParentRegionId",
                table: "Regions");
        }
    }
}
