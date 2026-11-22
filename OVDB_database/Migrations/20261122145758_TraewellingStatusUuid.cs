using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class TraewellingStatusUuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrawellingStatusUuid",
                table: "TrawellingInboxStatuses",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TrawellingStatusUuid",
                table: "RouteInstances",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingInboxStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingInboxStatuses",
                columns: new[] { "UserId", "TrawellingStatusUuid" });

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses",
                columns: new[] { "UserId", "TrawellingStatusUuid" });

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstances_TrawellingStatusUuid",
                table: "RouteInstances",
                column: "TrawellingStatusUuid");

            // Backfill from what is already stored: inbox payloads fetched raw from Träwelling carry the
            // status's uuid. Trips get it through their inbox row where one is left; the rest are filled by
            // a full Träwelling resync, or the next time their status comes by.
            migrationBuilder.Sql(@"
                UPDATE TrawellingInboxStatuses
                SET TrawellingStatusUuid = JSON_UNQUOTE(JSON_EXTRACT(PayloadJson, '$.uuid'))
                WHERE TrawellingStatusUuid IS NULL
                  AND JSON_VALID(PayloadJson)
                  AND JSON_EXTRACT(PayloadJson, '$.uuid') IS NOT NULL;");
            migrationBuilder.Sql(@"
                UPDATE RouteInstances ri
                JOIN TrawellingInboxStatuses i ON i.TrawellingStatusId = ri.TrawellingStatusId
                SET ri.TrawellingStatusUuid = i.TrawellingStatusUuid
                WHERE ri.TrawellingStatusUuid IS NULL
                  AND i.TrawellingStatusUuid IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrawellingInboxStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingInboxStatuses");

            migrationBuilder.DropIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses");

            migrationBuilder.DropIndex(
                name: "IX_RouteInstances_TrawellingStatusUuid",
                table: "RouteInstances");

            migrationBuilder.DropColumn(
                name: "TrawellingStatusUuid",
                table: "TrawellingInboxStatuses");

            migrationBuilder.DropColumn(
                name: "TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses");

            migrationBuilder.DropColumn(
                name: "TrawellingStatusUuid",
                table: "RouteInstances");
        }
    }
}
