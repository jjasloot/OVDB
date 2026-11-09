using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyColumnsAndTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InviteCodes");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                table: "Users");

            // Routes.FirstDateTime predates trips. The few legacy dates without a trip on that day
            // (10 in production, Nov 2026) become trips before the column goes, so no date is lost.
            migrationBuilder.Sql(@"
                INSERT INTO RouteInstances (RouteId, Date)
                SELECT r.RouteId, r.FirstDateTime
                FROM Routes r
                WHERE r.FirstDateTime IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM RouteInstances ri
                                  WHERE ri.RouteId = r.RouteId AND DATE(ri.Date) = DATE(r.FirstDateTime));");

            migrationBuilder.DropColumn(
                name: "FirstDateTime",
                table: "Routes");

            // Leftovers in the production schema that no model or migration has known about for
            // years: the pre-Regions country model, an empty test table, old text coordinates
            // (155 MB, replaced by LineString) and an unused Regions column. IF EXISTS because a
            // database created from these migrations never had them. Not restored by Down.
            migrationBuilder.Sql("DROP TABLE IF EXISTS RoutesCountries;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS Countries;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS Tags;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS test;");
            migrationBuilder.Sql("ALTER TABLE Routes DROP COLUMN IF EXISTS Coordinates;");
            migrationBuilder.Sql("ALTER TABLE Regions DROP COLUMN IF EXISTS LandMass;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstDateTime",
                table: "Routes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InviteCodes",
                columns: table => new
                {
                    InviteCodeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DoesNotExpire = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsUsed = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InviteCodes", x => x.InviteCodeId);
                    table.ForeignKey(
                        name: "FK_InviteCodes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InviteCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_Code",
                table: "InviteCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_CreatedByUserId",
                table: "InviteCodes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_UserId",
                table: "InviteCodes",
                column: "UserId");
        }
    }
}
