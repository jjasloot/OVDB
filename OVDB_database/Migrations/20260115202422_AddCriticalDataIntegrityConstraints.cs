using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OVDB_database.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalDataIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InviteCodes_Users_CreatedByUserId",
                table: "InviteCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_InviteCodes_Users_UserId",
                table: "InviteCodes");

            migrationBuilder.DropIndex(
                name: "IX_RouteInstanceProperties_RouteInstanceId",
                table: "RouteInstanceProperties");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Password",
                keyValue: null,
                column: "Password",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "varchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Email",
                keyValue: null,
                column: "Email",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "RouteInstanceProperties",
                keyColumn: "Key",
                keyValue: null,
                column: "Key",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "RouteInstanceProperties",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "InviteCodes",
                keyColumn: "Code",
                keyValue: null,
                column: "Code",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "InviteCodes",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Guid",
                table: "Users",
                column: "Guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramUserId",
                table: "Users",
                column: "TelegramUserId",
                unique: true,
                filter: "[TelegramUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StationMaps_MapGuid",
                table: "StationMaps",
                column: "MapGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationGroupings_MapGuid",
                table: "StationGroupings",
                column: "MapGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstanceProperties_RouteInstanceId_Key",
                table: "RouteInstanceProperties",
                columns: new[] { "RouteInstanceId", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_IsRevoked",
                table: "RefreshTokens",
                column: "IsRevoked");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_Code",
                table: "InviteCodes",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InviteCodes_Users_CreatedByUserId",
                table: "InviteCodes",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InviteCodes_Users_UserId",
                table: "InviteCodes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InviteCodes_Users_CreatedByUserId",
                table: "InviteCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_InviteCodes_Users_UserId",
                table: "InviteCodes");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Guid",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TelegramUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StationMaps_MapGuid",
                table: "StationMaps");

            migrationBuilder.DropIndex(
                name: "IX_StationGroupings_MapGuid",
                table: "StationGroupings");

            migrationBuilder.DropIndex(
                name: "IX_RouteInstanceProperties_RouteInstanceId_Key",
                table: "RouteInstanceProperties");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_IsRevoked",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_InviteCodes_Code",
                table: "InviteCodes");

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(512)",
                oldMaxLength: 512)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldMaxLength: 256)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "RouteInstanceProperties",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "InviteCodes",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RouteInstanceProperties_RouteInstanceId",
                table: "RouteInstanceProperties",
                column: "RouteInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_InviteCodes_Users_CreatedByUserId",
                table: "InviteCodes",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InviteCodes_Users_UserId",
                table: "InviteCodes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
