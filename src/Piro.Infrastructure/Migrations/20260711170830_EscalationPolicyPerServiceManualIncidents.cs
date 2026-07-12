using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EscalationPolicyPerServiceManualIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_EscalationPolicies_EscalationPolicyId",
                table: "Incidents");

            migrationBuilder.DropTable(
                name: "IncidentMerges");

            migrationBuilder.DropTable(
                name: "NotificationChannels");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_EscalationPolicyId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "EscalationCurrentStep",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "EscalationPolicyId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "EscalationStepStartedAt",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "LastUserActivityAt",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "CreateIncident",
                table: "AlertConfigs");

            migrationBuilder.DropColumn(
                name: "IncidentThresholdOccurrences",
                table: "AlertConfigs");

            migrationBuilder.AddColumn<int>(
                name: "EscalationPolicyId",
                table: "Services",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AcknowledgedAt",
                table: "Alerts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedBy",
                table: "Alerts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalationCurrentStep",
                table: "Alerts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalationStepStartedAt",
                table: "Alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUserActivityAt",
                table: "Alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Services_EscalationPolicyId",
                table: "Services",
                column: "EscalationPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_EscalationPolicies_EscalationPolicyId",
                table: "Services",
                column: "EscalationPolicyId",
                principalTable: "EscalationPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_EscalationPolicies_EscalationPolicyId",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_Services_EscalationPolicyId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "EscalationPolicyId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "AcknowledgedBy",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "EscalationCurrentStep",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "EscalationStepStartedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "LastUserActivityAt",
                table: "Alerts");

            migrationBuilder.AddColumn<int>(
                name: "EscalationCurrentStep",
                table: "Incidents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalationPolicyId",
                table: "Incidents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalationStepStartedAt",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUserActivityAt",
                table: "Incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreateIncident",
                table: "AlertConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IncidentThresholdOccurrences",
                table: "AlertConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "IncidentMerges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceIncidentId = table.Column<int>(type: "integer", nullable: false),
                    TargetIncidentId = table.Column<int>(type: "integer", nullable: false),
                    MergedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentMerges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentMerges_Incidents_SourceIncidentId",
                        column: x => x.SourceIncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentMerges_Incidents_TargetIncidentId",
                        column: x => x.TargetIncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntegrationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsGlobal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsInactive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MetaJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationChannels_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_EscalationPolicyId",
                table: "Incidents",
                column: "EscalationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentMerges_SourceIncidentId",
                table: "IncidentMerges",
                column: "SourceIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentMerges_TargetIncidentId",
                table: "IncidentMerges",
                column: "TargetIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannels_Id",
                table: "NotificationChannels",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannels_IntegrationId",
                table: "NotificationChannels",
                column: "IntegrationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_EscalationPolicies_EscalationPolicyId",
                table: "Incidents",
                column: "EscalationPolicyId",
                principalTable: "EscalationPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
