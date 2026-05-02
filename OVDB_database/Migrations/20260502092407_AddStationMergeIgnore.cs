using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddStationMergeIgnore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StationMergeIgnores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Station1Id = table.Column<int>(type: "int", nullable: false),
                    Station2Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationMergeIgnores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationMergeIgnores_Stations_Station1Id",
                        column: x => x.Station1Id,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StationMergeIgnores_Stations_Station2Id",
                        column: x => x.Station2Id,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StationMergeIgnores_Station1Id_Station2Id",
                table: "StationMergeIgnores",
                columns: new[] { "Station1Id", "Station2Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationMergeIgnores_Station2Id",
                table: "StationMergeIgnores",
                column: "Station2Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StationMergeIgnores");
        }
    }
}
