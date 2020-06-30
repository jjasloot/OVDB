using Microsoft.EntityFrameworkCore.Migrations;

namespace OVDB_database.Migrations
{
    public partial class AddrouteInstancesrelation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RouteInstances_Routes_RouteId",
                table: "RouteInstances");

            migrationBuilder.AlterColumn<int>(
                name: "RouteId",
                table: "RouteInstances",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RouteInstances_Routes_RouteId",
                table: "RouteInstances",
                column: "RouteId",
                principalTable: "Routes",
                principalColumn: "RouteId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RouteInstances_Routes_RouteId",
                table: "RouteInstances");

            migrationBuilder.AlterColumn<int>(
                name: "RouteId",
                table: "RouteInstances",
                type: "int",
                nullable: true,
                oldClrType: typeof(int));

            migrationBuilder.AddForeignKey(
                name: "FK_RouteInstances_Routes_RouteId",
                table: "RouteInstances",
                column: "RouteId",
                principalTable: "Routes",
                principalColumn: "RouteId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
