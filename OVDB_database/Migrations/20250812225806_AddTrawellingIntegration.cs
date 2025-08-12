using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddTrawellingIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrawellingAccessToken",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrawellingRefreshToken",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrawellingTokenExpiresAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrawellingStatusId",
                table: "RouteInstances",
                type: "INTEGER",
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
