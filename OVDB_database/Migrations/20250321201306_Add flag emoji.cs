using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class Addflagemoji : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagEmoji",
                table: "Regions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlagEmoji",
                table: "Regions");
        }
    }
}
