using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatortoroute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
