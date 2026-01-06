using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedToMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId",
                table: "TrawellingIgnoredStatuses");

            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "Maps",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusId",
                table: "TrawellingIgnoredStatuses",
                columns: new[] { "UserId", "TrawellingStatusId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trawelling_stations_traewelling_id",
                table: "trawelling_stations",
                column: "traewelling_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stations_OsmId",
                table: "Stations",
                column: "OsmId");

            migrationBuilder.CreateIndex(
                name: "IX_Routes_Share",
                table: "Routes",
                column: "Share");

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstances_TrawellingStatusId",
                table: "RouteInstances",
                column: "TrawellingStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_MapGuid",
                table: "Maps",
                column: "MapGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusId",
                table: "TrawellingIgnoredStatuses");

            migrationBuilder.DropIndex(
                name: "IX_trawelling_stations_traewelling_id",
                table: "trawelling_stations");

            migrationBuilder.DropIndex(
                name: "IX_Stations_OsmId",
                table: "Stations");

            migrationBuilder.DropIndex(
                name: "IX_Routes_Share",
                table: "Routes");

            migrationBuilder.DropIndex(
                name: "IX_RouteInstances_TrawellingStatusId",
                table: "RouteInstances");

            migrationBuilder.DropIndex(
                name: "IX_Maps_MapGuid",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "Completed",
                table: "Maps");

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId",
                table: "TrawellingIgnoredStatuses",
                column: "UserId");
        }
    }
}
