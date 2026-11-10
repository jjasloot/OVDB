using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class RepairRouteMapsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Routes left on no map by the old map delete (fixed in this release), which made them
            // invisible to their owners. Owners decided by hand from the surrounding routes
            // (Nov 2026). Matched on id and name and only while still on no map, so this does
            // nothing on any other database.
            foreach (var (routeId, name, userId) in new[]
            {
                (10268, "RE 21: Halberstadt Hbf => Wernigerode", 22),
                (10269, "Wernigerode => Nordhausen", 22),
                (10272, "Quedlinburg => Brocken", 22),
                (10273, "Brocken => Wernigerode", 22),
                (1488, "410: Leiden, Leiden Centraal => Leiderdorp, Ziekenhuis", 10),
            })
            {
                migrationBuilder.Sql($@"
                    INSERT INTO RoutesMaps (RouteId, MapId)
                    SELECT r.RouteId,
                           (SELECT m.MapId FROM Maps m WHERE m.UserId = {userId} ORDER BY m.`Default` DESC, m.OrderNr, m.MapId LIMIT 1)
                    FROM Routes r
                    WHERE r.RouteId = {routeId} AND TRIM(r.Name) = '{name}'
                      AND NOT EXISTS (SELECT 1 FROM RoutesMaps rm WHERE rm.RouteId = r.RouteId)
                      AND EXISTS (SELECT 1 FROM Maps m WHERE m.UserId = {userId});");
            }
            // The owner chose to delete this one; its trip, links and regions cascade.
            migrationBuilder.Sql(@"
                DELETE FROM Routes
                WHERE RouteId = 4886 AND TRIM(Name) = 'Groningen -> Zwolle (104,8km)'
                  AND NOT EXISTS (SELECT 1 FROM RoutesMaps rm WHERE rm.RouteId = 4886);");

            // One route was linked to the same map twice; keep the first link before the unique index.
            migrationBuilder.Sql(@"
                DELETE rm FROM RoutesMaps rm
                JOIN RoutesMaps keep ON keep.RouteId = rm.RouteId AND keep.MapId = rm.MapId AND keep.RouteMapId < rm.RouteMapId;");

            // Created before the old single-column index is dropped: the RouteId foreign key needs one of them.
            migrationBuilder.CreateIndex(
                name: "IX_RoutesMaps_RouteId_MapId",
                table: "RoutesMaps",
                columns: new[] { "RouteId", "MapId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_RoutesMaps_RouteId",
                table: "RoutesMaps");

            // Indexes fully covered by a wider one with the same leading column. The last two were
            // never in the model (an old raw-SQL migration, and drift), hence IF EXISTS.
            migrationBuilder.DropIndex(
                name: "IX_RouteInstances_Date",
                table: "RouteInstances");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_regionstation_regionid ON RegionStation;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_StationVisits_StationId ON StationVisits;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The data repair is not undone. Recreate before dropping: the foreign key needs an index.
            migrationBuilder.CreateIndex(
                name: "IX_RoutesMaps_RouteId",
                table: "RoutesMaps",
                column: "RouteId");

            migrationBuilder.DropIndex(
                name: "IX_RoutesMaps_RouteId_MapId",
                table: "RoutesMaps");

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstances_Date",
                table: "RouteInstances",
                column: "Date");
        }
    }
}
