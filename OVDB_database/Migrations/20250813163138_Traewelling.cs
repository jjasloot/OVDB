using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class Traewelling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrawellingAccessToken",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TrawellingRefreshToken",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "TrawellingTokenExpiresAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrawellingStatusId",
                table: "RouteInstances",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrawellingAccessToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrawellingRefreshToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrawellingTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrawellingStatusId",
                table: "RouteInstances");
        }
    }
}
