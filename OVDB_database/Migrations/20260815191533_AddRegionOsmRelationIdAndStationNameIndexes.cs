using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionOsmRelationIdAndStationNameIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Regions_OsmRelationId",
                table: "Regions",
                column: "OsmRelationId");

            // Stations.Name is longtext, so it needs a prefix-length index (which EF cannot
            // express in the model). This speeds up the by-name station lookups in the Trainlog
            // export and station imports, which currently full-scan the table.
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS IX_Stations_Name ON Stations (Name(255));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Regions_OsmRelationId",
                table: "Regions");

            migrationBuilder.Sql("ALTER TABLE Stations DROP INDEX IF EXISTS IX_Stations_Name;");
        }
    }
}
