using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleAlertFromCheckAndService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Checks_CheckId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Services_ServiceId",
                table: "Alerts");

            migrationBuilder.AddColumn<int>(
                name: "EscalationPolicyId",
                table: "Integrations",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ServiceId",
                table: "Alerts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "CheckId",
                table: "Alerts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "EscalationPolicyId",
                table: "Alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Alerts",
                type: "text",
                nullable: false,
                defaultValue: "Internal");

            migrationBuilder.AddColumn<int>(
                name: "SourceRequestLogId",
                table: "Alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WebhookRequestLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntegrationId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    AlertId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookRequestLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookRequestLogs_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_EscalationPolicyId",
                table: "Integrations",
                column: "EscalationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_EscalationPolicyId",
                table: "Alerts",
                column: "EscalationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_SourceRequestLogId",
                table: "Alerts",
                column: "SourceRequestLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookRequestLogs_IntegrationId_ReceivedAt",
                table: "WebhookRequestLogs",
                columns: new[] { "IntegrationId", "ReceivedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Checks_CheckId",
                table: "Alerts",
                column: "CheckId",
                principalTable: "Checks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_EscalationPolicies_EscalationPolicyId",
                table: "Alerts",
                column: "EscalationPolicyId",
                principalTable: "EscalationPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Services_ServiceId",
                table: "Alerts",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_WebhookRequestLogs_SourceRequestLogId",
                table: "Alerts",
                column: "SourceRequestLogId",
                principalTable: "WebhookRequestLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Integrations_EscalationPolicies_EscalationPolicyId",
                table: "Integrations",
                column: "EscalationPolicyId",
                principalTable: "EscalationPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Checks_CheckId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_EscalationPolicies_EscalationPolicyId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Services_ServiceId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_WebhookRequestLogs_SourceRequestLogId",
                table: "Alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Integrations_EscalationPolicies_EscalationPolicyId",
                table: "Integrations");

            migrationBuilder.DropTable(
                name: "WebhookRequestLogs");

            migrationBuilder.DropIndex(
                name: "IX_Integrations_EscalationPolicyId",
                table: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_EscalationPolicyId",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_SourceRequestLogId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "EscalationPolicyId",
                table: "Integrations");

            migrationBuilder.DropColumn(
                name: "EscalationPolicyId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "SourceRequestLogId",
                table: "Alerts");

            migrationBuilder.AlterColumn<int>(
                name: "ServiceId",
                table: "Alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CheckId",
                table: "Alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Checks_CheckId",
                table: "Alerts",
                column: "CheckId",
                principalTable: "Checks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Services_ServiceId",
                table: "Alerts",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
