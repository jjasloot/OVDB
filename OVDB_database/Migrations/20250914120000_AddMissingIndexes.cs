using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add index on RouteInstances.TrawellingStatusId
            migrationBuilder.CreateIndex(
                name: "IX_RouteInstances_TrawellingStatusId",
                table: "RouteInstances",
                column: "TrawellingStatusId");

            // Drop existing index on TrawellingIgnoredStatuses.UserId
            migrationBuilder.DropIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId",
                table: "TrawellingIgnoredStatuses");

            // Add unique composite index on TrawellingIgnoredStatuses(UserId, TrawellingStatusId)
            migrationBuilder.CreateIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusId",
                table: "TrawellingIgnoredStatuses",
                columns: new[] { "UserId", "TrawellingStatusId" },
                unique: true);

            // Add unique index on TrawellingStation.TrawellingId
            migrationBuilder.CreateIndex(
                name: "IX_trawelling_stations_traewelling_id",
                table: "trawelling_stations",
                column: "traewelling_id",
                unique: true);

            // Add index on Stations.OsmId
            migrationBuilder.CreateIndex(
                name: "IX_Stations_OsmId",
                table: "Stations",
                column: "OsmId");

            // Add unique index on Maps.MapGuid
            migrationBuilder.CreateIndex(
                name: "IX_Maps_MapGuid",
                table: "Maps",
                column: "MapGuid",
                unique: true);

            // Add index on Routes.Share
            migrationBuilder.CreateIndex(
                name: "IX_Routes_Share",
                table: "Routes",
                column: "Share");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop index on RouteInstances.TrawellingStatusId
            migrationBuilder.DropIndex(
                name: "IX_RouteInstances_TrawellingStatusId",
                table: "RouteInstances");

            // Drop composite index on TrawellingIgnoredStatuses
            migrationBuilder.DropIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusId",
                table: "TrawellingIgnoredStatuses");

            // Recreate single index on TrawellingIgnoredStatuses.UserId
            migrationBuilder.CreateIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId",
                table: "TrawellingIgnoredStatuses",
                column: "UserId");

            // Drop unique index on TrawellingStation.TrawellingId
            migrationBuilder.DropIndex(
                name: "IX_trawelling_stations_traewelling_id",
                table: "trawelling_stations");

            // Drop index on Stations.OsmId
            migrationBuilder.DropIndex(
                name: "IX_Stations_OsmId",
                table: "Stations");

            // Drop unique index on Maps.MapGuid
            migrationBuilder.DropIndex(
                name: "IX_Maps_MapGuid",
                table: "Maps");

            // Drop index on Routes.Share
            migrationBuilder.DropIndex(
                name: "IX_Routes_Share",
                table: "Routes");
        }
    }
}
