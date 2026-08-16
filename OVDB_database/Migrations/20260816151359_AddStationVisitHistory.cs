using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddStationVisitHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "StationVisits",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DatingSkipped",
                table: "StationVisits",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstEntryExitDate",
                table: "StationVisits",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstEntryExitRouteInstanceId",
                table: "StationVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstStoppedDate",
                table: "StationVisits",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstStoppedRouteInstanceId",
                table: "StationVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "StationVisits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StationSuggestionDismissals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DismissedOn = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationSuggestionDismissals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationSuggestionDismissals_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StationSuggestionDismissals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StationVisits_FirstEntryExitRouteInstanceId",
                table: "StationVisits",
                column: "FirstEntryExitRouteInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_StationVisits_FirstStoppedRouteInstanceId",
                table: "StationVisits",
                column: "FirstStoppedRouteInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_StationVisits_UserId_FirstEntryExitDate",
                table: "StationVisits",
                columns: new[] { "UserId", "FirstEntryExitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StationVisits_UserId_FirstStoppedDate",
                table: "StationVisits",
                columns: new[] { "UserId", "FirstStoppedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StationSuggestionDismissals_StationId",
                table: "StationSuggestionDismissals",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_StationSuggestionDismissals_UserId_StationId",
                table: "StationSuggestionDismissals",
                columns: new[] { "UserId", "StationId" },
                unique: true);

            // Moved after the new indexes are in place: UserId backs a foreign key to Users, and
            // MariaDB refuses to drop the last index covering it. The composite indexes above start
            // with UserId, so they take over that job - but only once they exist.
            migrationBuilder.DropIndex(
                name: "IX_StationVisits_UserId",
                table: "StationVisits");

            migrationBuilder.AddForeignKey(
                name: "FK_StationVisits_RouteInstances_FirstEntryExitRouteInstanceId",
                table: "StationVisits",
                column: "FirstEntryExitRouteInstanceId",
                principalTable: "RouteInstances",
                principalColumn: "RouteInstanceId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StationVisits_RouteInstances_FirstStoppedRouteInstanceId",
                table: "StationVisits",
                column: "FirstStoppedRouteInstanceId",
                principalTable: "RouteInstances",
                principalColumn: "RouteInstanceId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StationVisits_RouteInstances_FirstEntryExitRouteInstanceId",
                table: "StationVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_StationVisits_RouteInstances_FirstStoppedRouteInstanceId",
                table: "StationVisits");

            migrationBuilder.DropTable(
                name: "StationSuggestionDismissals");

            migrationBuilder.DropIndex(
                name: "IX_StationVisits_FirstEntryExitRouteInstanceId",
                table: "StationVisits");

            migrationBuilder.DropIndex(
                name: "IX_StationVisits_FirstStoppedRouteInstanceId",
                table: "StationVisits");

            migrationBuilder.DropIndex(
                name: "IX_StationVisits_UserId_FirstEntryExitDate",
                table: "StationVisits");

            migrationBuilder.DropIndex(
                name: "IX_StationVisits_UserId_FirstStoppedDate",
                table: "StationVisits");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "StationVisits");

            migrationBuilder.DropColumn(
                name: "DatingSkipped",
                table: "StationVisits");

            migrationBuilder.DropColumn(
                name: "FirstEntryExitDate",
                table: "StationVisits");

            migrationBuilder.DropColumn(
                name: "FirstEntryExitRouteInstanceId",
                table: "StationVisits");

            migrationBuilder.DropColumn(
                name: "FirstStoppedDate",
                table: "StationVisits");

            migrationBuilder.DropColumn(
                name: "FirstStoppedRouteInstanceId",
                table: "StationVisits");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "StationVisits");

            migrationBuilder.CreateIndex(
                name: "IX_StationVisits_UserId",
                table: "StationVisits",
                column: "UserId");
        }
    }
}
