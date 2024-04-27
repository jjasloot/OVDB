using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class MakeStationCountryIdoptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stations_StationCountries_StationCountryId",
                table: "Stations");

            migrationBuilder.AlterColumn<int>(
                name: "StationCountryId",
                table: "Stations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_StationCountries_StationCountryId",
                table: "Stations",
                column: "StationCountryId",
                principalTable: "StationCountries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stations_StationCountries_StationCountryId",
                table: "Stations");

            migrationBuilder.AlterColumn<int>(
                name: "StationCountryId",
                table: "Stations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_StationCountries_StationCountryId",
                table: "Stations",
                column: "StationCountryId",
                principalTable: "StationCountries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
