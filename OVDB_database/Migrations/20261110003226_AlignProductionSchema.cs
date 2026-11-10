using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AlignProductionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Production had drifted from what the migrations create (found by comparing a copy of
            // it with a database built from the migrations, Nov 2026). Every statement here is a
            // no-op on a database built from the migrations.

            // TrawellingTokenExpiresAt was text (compared with dates only through implicit casts),
            // and the token columns 3-byte utf8; all values convert cleanly.
            migrationBuilder.Sql(@"
                ALTER TABLE Users
                    MODIFY TrawellingAccessToken longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                    MODIFY TrawellingRefreshToken longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                    MODIFY TrawellingUsername longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
                    MODIFY TrawellingTokenExpiresAt datetime(6) NULL;");

            // 0 was the old 'not set'; it blocked the unique index the bot's lookup relies on.
            migrationBuilder.Sql("UPDATE Users SET TelegramUserId = NULL WHERE TelegramUserId = 0;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_TelegramUserId ON Users (TelegramUserId);");

            // One ignore per user and status, and the lookup index; the old one is then redundant.
            migrationBuilder.Sql(@"
                DELETE i FROM TrawellingIgnoredStatuses i
                JOIN TrawellingIgnoredStatuses keep ON keep.UserId = i.UserId AND keep.TrawellingStatusId = i.TrawellingStatusId AND keep.Id < i.Id;");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusId ON TrawellingIgnoredStatuses (UserId, TrawellingStatusId);");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_TrawellingIgnoredStatuses_UserId ON TrawellingIgnoredStatuses;");

            // Covered by IX_RouteInstanceProperties_RouteInstanceId_Key.
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_RouteInstanceProperties_RouteInstanceId ON RouteInstanceProperties;");

            // Table defaults only (metadata): a column added later without an explicit character
            // set would otherwise come out 3-byte. Existing columns keep their character set.
            foreach (var table in new[] { "RouteInstances", "Routes", "Maps", "RouteInstanceProperties", "RouteTypes", "StationVisits", "RoutesMaps", "Stations", "RouteInstanceMap", "Users", "StationMaps" })
            {
                migrationBuilder.Sql($"ALTER TABLE {table} DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversed: it only brings a drifted database in line with the migrations.
        }
    }
}
