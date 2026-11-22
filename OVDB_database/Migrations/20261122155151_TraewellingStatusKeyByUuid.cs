using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class TraewellingStatusKeyByUuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrawellingInboxStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingInboxStatuses");

            migrationBuilder.DropIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses");

            migrationBuilder.AlterColumn<int>(
                name: "TrawellingStatusId",
                table: "TrawellingInboxStatuses",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "TrawellingStatusId",
                table: "TrawellingIgnoredStatuses",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Negative ids were OVDB's own stand-ins for statuses that came with only a UUID; the UUID is the key now.
            migrationBuilder.Sql("UPDATE TrawellingInboxStatuses SET TrawellingStatusId = NULL WHERE TrawellingStatusId < 0;");
            migrationBuilder.Sql("UPDATE TrawellingIgnoredStatuses SET TrawellingStatusId = NULL WHERE TrawellingStatusId < 0;");
            migrationBuilder.Sql("UPDATE RouteInstances SET TrawellingStatusId = NULL WHERE TrawellingStatusId < 0;");

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingInboxStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingInboxStatuses",
                columns: new[] { "UserId", "TrawellingStatusUuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses",
                columns: new[] { "UserId", "TrawellingStatusUuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrawellingInboxStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingInboxStatuses");

            migrationBuilder.DropIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses");

            migrationBuilder.AlterColumn<int>(
                name: "TrawellingStatusId",
                table: "TrawellingInboxStatuses",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TrawellingStatusId",
                table: "TrawellingIgnoredStatuses",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingInboxStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingInboxStatuses",
                columns: new[] { "UserId", "TrawellingStatusUuid" });

            migrationBuilder.CreateIndex(
                name: "IX_TrawellingIgnoredStatuses_UserId_TrawellingStatusUuid",
                table: "TrawellingIgnoredStatuses",
                columns: new[] { "UserId", "TrawellingStatusUuid" });
        }
    }
}
