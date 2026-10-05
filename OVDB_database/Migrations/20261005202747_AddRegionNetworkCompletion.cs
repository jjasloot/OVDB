using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionNetworkCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MatchesRailNetworks",
                table: "RouteTypes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // One-off seed of the OVDB-native rail-bound flag from what exists today: the stats
            // flag, Trainlog's export type where set, and otherwise the type's name. Runs exactly
            // once here rather than at startup, and is idempotent on the flag itself. The route
            // types UI owns the value from here on.
            migrationBuilder.Sql(@"
UPDATE RouteTypes
SET MatchesRailNetworks = 1
WHERE MatchesRailNetworks = 0 AND (
       IsTrain = 1
    OR LOWER(COALESCE(TrainlogType, '')) IN ('train', 'tram', 'metro')
    OR LOWER(Name) REGEXP 'trein|train|zug|rail|tram|metro|subway|u-bahn|s-bahn|stadtbahn|light rail'
);");

            migrationBuilder.CreateTable(
                name: "NetworkWayOverrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RegionId = table.Column<int>(type: "int", nullable: false),
                    OsmWayId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkWayOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkWayOverrides_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RegionNetworks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RegionId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssembledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OsmDataTimestamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EdgeCount = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TotalKm = table.Column<double>(type: "double", nullable: false),
                    BonusKm = table.Column<double>(type: "double", nullable: false),
                    ClassifiedByRelations = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Error = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionNetworks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegionNetworks_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NetworkEdges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RegionNetworkId = table.Column<int>(type: "int", nullable: false),
                    Geometry = table.Column<LineString>(type: "linestring", nullable: true),
                    SimplifiedGeometry = table.Column<LineString>(type: "linestring", nullable: true),
                    HiddenTwins = table.Column<MultiLineString>(type: "multilinestring", nullable: true),
                    LengthKm = table.Column<double>(type: "double", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    IsPassenger = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromStation = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ToStation = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OsmWayIds = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkEdges_RegionNetworks_RegionNetworkId",
                        column: x => x.RegionNetworkId,
                        principalTable: "RegionNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RouteNetworkEdges",
                columns: table => new
                {
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    NetworkEdgeId = table.Column<long>(type: "bigint", nullable: false),
                    CoveredSamples = table.Column<byte[]>(type: "longblob", nullable: true),
                    CoveredCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteNetworkEdges", x => new { x.RouteId, x.NetworkEdgeId });
                    table.ForeignKey(
                        name: "FK_RouteNetworkEdges_NetworkEdges_NetworkEdgeId",
                        column: x => x.NetworkEdgeId,
                        principalTable: "NetworkEdges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteNetworkEdges_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEdges_RegionNetworkId",
                table: "NetworkEdges",
                column: "RegionNetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkWayOverrides_RegionId_OsmWayId",
                table: "NetworkWayOverrides",
                columns: new[] { "RegionId", "OsmWayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegionNetworks_RegionId_Mode",
                table: "RegionNetworks",
                columns: new[] { "RegionId", "Mode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteNetworkEdges_NetworkEdgeId",
                table: "RouteNetworkEdges",
                column: "NetworkEdgeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkWayOverrides");

            migrationBuilder.DropTable(
                name: "RouteNetworkEdges");

            migrationBuilder.DropTable(
                name: "NetworkEdges");

            migrationBuilder.DropTable(
                name: "RegionNetworks");

            migrationBuilder.DropColumn(
                name: "MatchesRailNetworks",
                table: "RouteTypes");
        }
    }
}
