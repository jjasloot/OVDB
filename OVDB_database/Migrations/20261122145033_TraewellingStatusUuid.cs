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
                table: "RouteInstances",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Backfill from what is already stored: inbox payloads fetched raw from Träwelling carry the
            // status's uuid. Imported trips get it through their inbox row where one is left; for the
            // rest it stays null (their rows were deleted on import) until they are next synced.
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
            migrationBuilder.DropColumn(
                name: "TrawellingStatusUuid",
                table: "TrawellingInboxStatuses");

            migrationBuilder.DropColumn(
                name: "TrawellingStatusUuid",
                table: "RouteInstances");
        }
    }
}
