using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddRunsTrainsInRegionsandRestrictToRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperatorRegion_Operators_OperatorsId",
                table: "OperatorRegion");

            migrationBuilder.DropForeignKey(
                name: "FK_OperatorRegion_Regions_RegionsId",
                table: "OperatorRegion");

            migrationBuilder.RenameColumn(
                name: "RegionsId",
                table: "OperatorRegion",
                newName: "RunsTrainsInRegionsId");

            migrationBuilder.RenameColumn(
                name: "OperatorsId",
                table: "OperatorRegion",
                newName: "OperatorsRunningTrainsId");

            migrationBuilder.RenameIndex(
                name: "IX_OperatorRegion_RegionsId",
                table: "OperatorRegion",
                newName: "IX_OperatorRegion_RunsTrainsInRegionsId");

            migrationBuilder.CreateTable(
                name: "OperatorRegion1",
                columns: table => new
                {
                    OperatorsRestrictedToRegionId = table.Column<int>(type: "int", nullable: false),
                    RestrictToRegionsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorRegion1", x => new { x.OperatorsRestrictedToRegionId, x.RestrictToRegionsId });
                    table.ForeignKey(
                        name: "FK_OperatorRegion1_Operators_OperatorsRestrictedToRegionId",
                        column: x => x.OperatorsRestrictedToRegionId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorRegion1_Regions_RestrictToRegionsId",
                        column: x => x.RestrictToRegionsId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorRegion1_RestrictToRegionsId",
                table: "OperatorRegion1",
                column: "RestrictToRegionsId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperatorRegion_Operators_OperatorsRunningTrainsId",
                table: "OperatorRegion",
                column: "OperatorsRunningTrainsId",
                principalTable: "Operators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OperatorRegion_Regions_RunsTrainsInRegionsId",
                table: "OperatorRegion",
                column: "RunsTrainsInRegionsId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperatorRegion_Operators_OperatorsRunningTrainsId",
                table: "OperatorRegion");

            migrationBuilder.DropForeignKey(
                name: "FK_OperatorRegion_Regions_RunsTrainsInRegionsId",
                table: "OperatorRegion");

            migrationBuilder.DropTable(
                name: "OperatorRegion1");

            migrationBuilder.RenameColumn(
                name: "RunsTrainsInRegionsId",
                table: "OperatorRegion",
                newName: "RegionsId");

            migrationBuilder.RenameColumn(
                name: "OperatorsRunningTrainsId",
                table: "OperatorRegion",
                newName: "OperatorsId");

            migrationBuilder.RenameIndex(
                name: "IX_OperatorRegion_RunsTrainsInRegionsId",
                table: "OperatorRegion",
                newName: "IX_OperatorRegion_RegionsId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperatorRegion_Operators_OperatorsId",
                table: "OperatorRegion",
                column: "OperatorsId",
                principalTable: "Operators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OperatorRegion_Regions_RegionsId",
                table: "OperatorRegion",
                column: "RegionsId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
