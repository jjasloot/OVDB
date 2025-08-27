using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class Addindex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
 
            migrationBuilder.CreateIndex(
                name: "idx_routeinstances_date_routeid",
                table: "RouteInstances",
                columns: new[] { "Date", "RouteId" });

            migrationBuilder.CreateIndex(
                name: "idx_routeinstances_routeid_date",
                table: "RouteInstances",
                columns: new[] { "RouteId", "Date" });

            migrationBuilder.CreateIndex(
                name: "idx_routeinstancemap_routeinstanceid_mapid",
                table: "RouteInstanceMap",
                columns: new[] { "RouteInstanceId", "MapId" });

            migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_regionstation_regionid 
            ON RegionStation(RegionsId)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_routeinstances_date_routeid",
                table: "RouteInstances");

            migrationBuilder.DropIndex(
                name: "idx_routeinstances_routeid_date",
                table: "RouteInstances");

            migrationBuilder.DropIndex(
                name: "idx_routeinstancemap_routeinstanceid_mapid",
                table: "RouteInstanceMap");

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstances_RouteId",
                table: "RouteInstances",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstanceMap_RouteInstanceId",
                table: "RouteInstanceMap",
                column: "RouteInstanceId");
                    migrationBuilder.Sql("DROP INDEX IF EXISTS idx_regionstation_regionid");
        }
    }
}
