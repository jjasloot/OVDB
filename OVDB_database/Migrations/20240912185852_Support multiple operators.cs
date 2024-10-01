using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class Supportmultipleoperators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Routes_Operators_OperatorId",
                table: "Routes");

            migrationBuilder.DropIndex(
                name: "IX_Routes_OperatorId",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "OperatorId",
                table: "Routes");

            migrationBuilder.CreateTable(
                name: "OperatorRoute",
                columns: table => new
                {
                    OperatorsId = table.Column<int>(type: "int", nullable: false),
                    RoutesRouteId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorRoute", x => new { x.OperatorsId, x.RoutesRouteId });
                    table.ForeignKey(
                        name: "FK_OperatorRoute_Operators_OperatorsId",
                        column: x => x.OperatorsId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorRoute_Routes_RoutesRouteId",
                        column: x => x.RoutesRouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorRoute_RoutesRouteId",
                table: "OperatorRoute",
                column: "RoutesRouteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorRoute");

            migrationBuilder.AddColumn<int>(
                name: "OperatorId",
                table: "Routes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routes_OperatorId",
                table: "Routes",
                column: "OperatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Routes_Operators_OperatorId",
                table: "Routes",
                column: "OperatorId",
                principalTable: "Operators",
                principalColumn: "Id");
        }
    }
}
