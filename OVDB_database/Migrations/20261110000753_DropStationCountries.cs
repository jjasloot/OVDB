using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class DropStationCountries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stations_StationCountries_StationCountryId",
                table: "Stations");

            migrationBuilder.DropTable(
                name: "StationMapCountries");

            migrationBuilder.DropTable(
                name: "StationCountries");

            migrationBuilder.DropIndex(
                name: "IX_Stations_StationCountryId",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "StationCountryId",
                table: "Stations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StationCountryId",
                table: "Stations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StationCountries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NameNL = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OsmId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationCountries", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StationMapCountries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StationCountryId = table.Column<int>(type: "int", nullable: false),
                    StationMapId = table.Column<int>(type: "int", nullable: false),
                    IncludeSpecials = table.Column<bool>(type: "tinyint(1)", nullable: false)
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
                        principalColumn: "StationMapId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Stations_StationCountryId",
                table: "Stations",
                column: "StationCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_StationMapCountries_StationCountryId",
                table: "StationMapCountries",
                column: "StationCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_StationMapCountries_StationMapId",
                table: "StationMapCountries",
                column: "StationMapId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_StationCountries_StationCountryId",
                table: "Stations",
                column: "StationCountryId",
                principalTable: "StationCountries",
                principalColumn: "Id");
        }
    }
}
