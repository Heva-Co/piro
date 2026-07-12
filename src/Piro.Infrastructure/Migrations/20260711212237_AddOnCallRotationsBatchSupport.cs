using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnCallRotationsBatchSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserNotificationPreferences_Integrations_IntegrationId",
                table: "UserNotificationPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserNotificationPreferences_UserId_IntegrationId",
                table: "UserNotificationPreferences");

            migrationBuilder.AlterColumn<int>(
                name: "IntegrationId",
                table: "UserNotificationPreferences",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "UserNotificationPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationPreferences_UserId_Channel_IntegrationId",
                table: "UserNotificationPreferences",
                columns: new[] { "UserId", "Channel", "IntegrationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotificationPreferences_Integrations_IntegrationId",
                table: "UserNotificationPreferences",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserNotificationPreferences_Integrations_IntegrationId",
                table: "UserNotificationPreferences");

            migrationBuilder.DropIndex(
                name: "IX_UserNotificationPreferences_UserId_Channel_IntegrationId",
                table: "UserNotificationPreferences");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "UserNotificationPreferences");

            migrationBuilder.AlterColumn<int>(
                name: "IntegrationId",
                table: "UserNotificationPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationPreferences_UserId_IntegrationId",
                table: "UserNotificationPreferences",
                columns: new[] { "UserId", "IntegrationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotificationPreferences_Integrations_IntegrationId",
                table: "UserNotificationPreferences",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
