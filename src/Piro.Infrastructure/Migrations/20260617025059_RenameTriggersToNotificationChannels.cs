using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTriggersToNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FK constraints referencing the old tables/columns before renaming
            migrationBuilder.DropForeignKey(
                name: "FK_AlertConfigTriggers_AlertConfigs_AlertConfigId",
                table: "AlertConfigTriggers");

            migrationBuilder.DropForeignKey(
                name: "FK_AlertConfigTriggers_Triggers_TriggerId",
                table: "AlertConfigTriggers");

            // Drop old primary key and index on join table
            migrationBuilder.DropPrimaryKey(
                name: "PK_AlertConfigTriggers",
                table: "AlertConfigTriggers");

            migrationBuilder.DropIndex(
                name: "IX_AlertConfigTriggers_TriggerId",
                table: "AlertConfigTriggers");

            // Drop old index on Triggers
            migrationBuilder.DropIndex(
                name: "IX_Triggers_Id",
                table: "Triggers");

            // Rename Triggers → NotificationChannels
            migrationBuilder.RenameTable(
                name: "Triggers",
                newName: "NotificationChannels");

            // Rename AlertConfigTriggers → AlertConfigNotificationChannels
            migrationBuilder.RenameTable(
                name: "AlertConfigTriggers",
                newName: "AlertConfigNotificationChannels");

            // Rename TriggerId → NotificationChannelId in the join table
            migrationBuilder.RenameColumn(
                name: "TriggerId",
                table: "AlertConfigNotificationChannels",
                newName: "NotificationChannelId");

            // Re-create primary key on the renamed join table
            migrationBuilder.AddPrimaryKey(
                name: "PK_AlertConfigNotificationChannels",
                table: "AlertConfigNotificationChannels",
                columns: new[] { "AlertConfigId", "NotificationChannelId" });

            // Re-create index on new column name
            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigNotificationChannels_NotificationChannelId",
                table: "AlertConfigNotificationChannels",
                column: "NotificationChannelId");

            // Re-create index on new table
            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannels_Id",
                table: "NotificationChannels",
                column: "Id");

            // Re-add FK constraints with new names
            migrationBuilder.AddForeignKey(
                name: "FK_AlertConfigNotificationChannels_AlertConfigs_AlertConfigId",
                table: "AlertConfigNotificationChannels",
                column: "AlertConfigId",
                principalTable: "AlertConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlertConfigNotificationChannels_NotificationChannels_Notifi~",
                table: "AlertConfigNotificationChannels",
                column: "NotificationChannelId",
                principalTable: "NotificationChannels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FK constraints
            migrationBuilder.DropForeignKey(
                name: "FK_AlertConfigNotificationChannels_AlertConfigs_AlertConfigId",
                table: "AlertConfigNotificationChannels");

            migrationBuilder.DropForeignKey(
                name: "FK_AlertConfigNotificationChannels_NotificationChannels_Notifi~",
                table: "AlertConfigNotificationChannels");

            // Drop primary key and index
            migrationBuilder.DropPrimaryKey(
                name: "PK_AlertConfigNotificationChannels",
                table: "AlertConfigNotificationChannels");

            migrationBuilder.DropIndex(
                name: "IX_AlertConfigNotificationChannels_NotificationChannelId",
                table: "AlertConfigNotificationChannels");

            migrationBuilder.DropIndex(
                name: "IX_NotificationChannels_Id",
                table: "NotificationChannels");

            // Rename back
            migrationBuilder.RenameColumn(
                name: "NotificationChannelId",
                table: "AlertConfigNotificationChannels",
                newName: "TriggerId");

            migrationBuilder.RenameTable(
                name: "AlertConfigNotificationChannels",
                newName: "AlertConfigTriggers");

            migrationBuilder.RenameTable(
                name: "NotificationChannels",
                newName: "Triggers");

            // Re-create old primary key
            migrationBuilder.AddPrimaryKey(
                name: "PK_AlertConfigTriggers",
                table: "AlertConfigTriggers",
                columns: new[] { "AlertConfigId", "TriggerId" });

            // Re-create old indexes
            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigTriggers_TriggerId",
                table: "AlertConfigTriggers",
                column: "TriggerId");

            migrationBuilder.CreateIndex(
                name: "IX_Triggers_Id",
                table: "Triggers",
                column: "Id");

            // Re-add old FK constraints
            migrationBuilder.AddForeignKey(
                name: "FK_AlertConfigTriggers_AlertConfigs_AlertConfigId",
                table: "AlertConfigTriggers",
                column: "AlertConfigId",
                principalTable: "AlertConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AlertConfigTriggers_Triggers_TriggerId",
                table: "AlertConfigTriggers",
                column: "TriggerId",
                principalTable: "Triggers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
