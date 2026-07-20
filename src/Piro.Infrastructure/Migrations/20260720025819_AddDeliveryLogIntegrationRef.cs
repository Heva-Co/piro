using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryLogIntegrationRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationId",
                table: "NotificationDeliveryLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationType",
                table: "NotificationDeliveryLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryLogs_IntegrationId",
                table: "NotificationDeliveryLogs",
                column: "IntegrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationDeliveryLogs_IntegrationId",
                table: "NotificationDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "IntegrationId",
                table: "NotificationDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "IntegrationType",
                table: "NotificationDeliveryLogs");
        }
    }
}
