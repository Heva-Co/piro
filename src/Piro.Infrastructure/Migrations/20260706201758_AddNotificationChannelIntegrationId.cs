using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationChannelIntegrationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntegrationId",
                table: "NotificationChannels",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannels_IntegrationId",
                table: "NotificationChannels",
                column: "IntegrationId");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationChannels_Integrations_IntegrationId",
                table: "NotificationChannels",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationChannels_Integrations_IntegrationId",
                table: "NotificationChannels");

            migrationBuilder.DropIndex(
                name: "IX_NotificationChannels_IntegrationId",
                table: "NotificationChannels");

            migrationBuilder.DropColumn(
                name: "IntegrationId",
                table: "NotificationChannels");
        }
    }
}
