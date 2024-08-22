using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class CorrectoperatorstouseRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CountryOperator");

            migrationBuilder.CreateTable(
                name: "OperatorRegion",
                columns: table => new
                {
                    OperatorsId = table.Column<int>(type: "int", nullable: false),
                    RegionsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorRegion", x => new { x.OperatorsId, x.RegionsId });
                    table.ForeignKey(
                        name: "FK_OperatorRegion_Operators_OperatorsId",
                        column: x => x.OperatorsId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorRegion_Regions_RegionsId",
                        column: x => x.RegionsId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorRegion_RegionsId",
                table: "OperatorRegion",
                column: "RegionsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorRegion");

            migrationBuilder.CreateTable(
                name: "CountryOperator",
                columns: table => new
                {
                    CountriesCountryId = table.Column<int>(type: "int", nullable: false),
                    OperatorsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountryOperator", x => new { x.CountriesCountryId, x.OperatorsId });
                    table.ForeignKey(
                        name: "FK_CountryOperator_Countries_CountriesCountryId",
                        column: x => x.CountriesCountryId,
                        principalTable: "Countries",
                        principalColumn: "CountryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CountryOperator_Operators_OperatorsId",
                        column: x => x.OperatorsId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CountryOperator_OperatorsId",
                table: "CountryOperator",
                column: "OperatorsId");
        }
    }
}
