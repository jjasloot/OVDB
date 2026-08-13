using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class TraewellingInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TrawellingLastSweepAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrawellingInboxStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TrawellingStatusId = table.Column<int>(type: "int", nullable: false),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    DepartureAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastEventAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrawellingInboxStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrawellingInboxStatuses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingInboxStatuses_UserId_TrawellingStatusId",
                table: "TrawellingInboxStatuses",
                columns: new[] { "UserId", "TrawellingStatusId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrawellingInboxStatuses");

            migrationBuilder.DropColumn(
                name: "TrawellingLastSweepAt",
                table: "Users");
        }
    }
}
