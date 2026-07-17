using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationIdToGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Integer PKs/FKs can't be cast directly to uuid, so each old int id is remapped to a
            // deterministic new Guid (stashed in a temp mapping table) before the int columns are
            // dropped — this preserves every existing Integration and its Check/preference links.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "_IntegrationIdMap" AS
                SELECT "Id" AS "OldId", gen_random_uuid() AS "NewId" FROM "Integrations";
            """);

            migrationBuilder.AddColumn<Guid>(
                name: "NewId",
                table: "Integrations",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
            migrationBuilder.Sql("""
                UPDATE "Integrations" i SET "NewId" = m."NewId"
                FROM "_IntegrationIdMap" m WHERE m."OldId" = i."Id";
            """);

            migrationBuilder.AddColumn<Guid>(
                name: "NewIntegrationId",
                table: "Checks",
                type: "uuid",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "Checks" c SET "NewIntegrationId" = m."NewId"
                FROM "_IntegrationIdMap" m WHERE m."OldId" = c."IntegrationId";
            """);

            migrationBuilder.AddColumn<Guid>(
                name: "NewIntegrationId",
                table: "UserNotificationPreferences",
                type: "uuid",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "UserNotificationPreferences" p SET "NewIntegrationId" = m."NewId"
                FROM "_IntegrationIdMap" m WHERE m."OldId" = p."IntegrationId";
            """);

            migrationBuilder.AddColumn<Guid>(
                name: "NewIntegrationId",
                table: "WebhookRequestLogs",
                type: "uuid",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "WebhookRequestLogs" w SET "NewIntegrationId" = m."NewId"
                FROM "_IntegrationIdMap" m WHERE m."OldId" = w."IntegrationId";
            """);

            migrationBuilder.DropForeignKey(name: "FK_Checks_Integrations_IntegrationId", table: "Checks");
            migrationBuilder.DropForeignKey(name: "FK_UserNotificationPreferences_Integrations_IntegrationId", table: "UserNotificationPreferences");
            migrationBuilder.DropForeignKey(name: "FK_WebhookRequestLogs_Integrations_IntegrationId", table: "WebhookRequestLogs");
            migrationBuilder.DropPrimaryKey(name: "PK_Integrations", table: "Integrations");

            migrationBuilder.DropColumn(name: "Id", table: "Integrations");
            migrationBuilder.DropColumn(name: "IntegrationId", table: "Checks");
            migrationBuilder.DropColumn(name: "IntegrationId", table: "UserNotificationPreferences");
            migrationBuilder.DropColumn(name: "IntegrationId", table: "WebhookRequestLogs");

            migrationBuilder.RenameColumn(name: "NewId", table: "Integrations", newName: "Id");
            migrationBuilder.RenameColumn(name: "NewIntegrationId", table: "Checks", newName: "IntegrationId");
            migrationBuilder.RenameColumn(name: "NewIntegrationId", table: "UserNotificationPreferences", newName: "IntegrationId");
            migrationBuilder.RenameColumn(name: "NewIntegrationId", table: "WebhookRequestLogs", newName: "IntegrationId");

            migrationBuilder.AddPrimaryKey(name: "PK_Integrations", table: "Integrations", column: "Id");

            migrationBuilder.AlterColumn<Guid>(
                name: "IntegrationId",
                table: "WebhookRequestLogs",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Checks_Integrations_IntegrationId",
                table: "Checks",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserNotificationPreferences_Integrations_IntegrationId",
                table: "UserNotificationPreferences",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WebhookRequestLogs_Integrations_IntegrationId",
                table: "WebhookRequestLogs",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "IntegrationIdToGuid cannot be reverted: the original int identity sequence for " +
                "Integrations.Id is not preserved. Restore from a backup taken before this migration instead.");
        }
    }
}
