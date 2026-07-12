using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertEntityAndRemoveGlobalIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertConfigNotificationChannels");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "Incidents");

            migrationBuilder.AddColumn<int>(
                name: "AlertId",
                table: "IncidentTimelineEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncidentThresholdOccurrences",
                table: "AlertConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Data migration: preserve automatic-incident-creation behavior for existing checks.
            // Any check that had AutomaticallyCreateIncident=true but has no AlertConfig with
            // CreateIncident=true yet gets one created, so this schema change doesn't silently
            // disable incident automation for checks already configured in production.
            // Must run after the columns it reads/writes are added, and before the Check columns
            // it reads (AutomaticallyCreateIncident, Criticality) are dropped below.
            migrationBuilder.Sql(@"
                INSERT INTO ""AlertConfigs"" (
                    ""CheckId"", ""AlertFor"", ""AlertValue"", ""FailureThreshold"", ""SuccessThreshold"",
                    ""Description"", ""CreateIncident"", ""IncidentThresholdOccurrences"", ""IsActive"", ""Severity"", ""IsAlerting"",
                    ""CreatedAt"", ""UpdatedAt""
                )
                SELECT
                    c.""Id"", 'Status', 'DOWN', COALESCE(c.""FailureThreshold"", 1), COALESCE(c.""RecoveryThreshold"", 1),
                    'Auto-migrated from Check.AutomaticallyCreateIncident', true, 1, true,
                    CASE WHEN c.""Criticality"" = 'Critical' THEN 'Critical' ELSE 'Warning' END, false,
                    now(), now()
                FROM ""Checks"" c
                WHERE c.""AutomaticallyCreateIncident"" = true
                  AND NOT EXISTS (
                      SELECT 1 FROM ""AlertConfigs"" ac WHERE ac.""CheckId"" = c.""Id"" AND ac.""CreateIncident"" = true
                  );
            ");

            migrationBuilder.DropColumn(
                name: "AutomaticallyCreateIncident",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "Criticality",
                table: "Checks");

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertConfigId = table.Column<int>(type: "integer", nullable: false),
                    CheckId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    IncidentId = table.Column<int>(type: "integer", nullable: true),
                    ImpactAtFireTime = table.Column<string>(type: "text", nullable: false, defaultValue: "DOWN"),
                    Message = table.Column<string>(type: "text", nullable: true),
                    MessageFingerprint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_AlertConfigs_AlertConfigId",
                        column: x => x.AlertConfigId,
                        principalTable: "AlertConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Alerts_Checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "Checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Alerts_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_AlertConfigId_ResolvedAt",
                table: "Alerts",
                columns: new[] { "AlertConfigId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_CheckId",
                table: "Alerts",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IncidentId",
                table: "Alerts",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_ServiceId_ResolvedAt",
                table: "Alerts",
                columns: new[] { "ServiceId", "ResolvedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropColumn(
                name: "AlertId",
                table: "IncidentTimelineEvents");

            migrationBuilder.DropColumn(
                name: "IncidentThresholdOccurrences",
                table: "AlertConfigs");

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "Incidents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutomaticallyCreateIncident",
                table: "Checks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Criticality",
                table: "Checks",
                type: "text",
                nullable: false,
                defaultValue: "High");

            migrationBuilder.CreateTable(
                name: "AlertConfigNotificationChannels",
                columns: table => new
                {
                    AlertConfigId = table.Column<int>(type: "integer", nullable: false),
                    NotificationChannelId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertConfigNotificationChannels", x => new { x.AlertConfigId, x.NotificationChannelId });
                    table.ForeignKey(
                        name: "FK_AlertConfigNotificationChannels_AlertConfigs_AlertConfigId",
                        column: x => x.AlertConfigId,
                        principalTable: "AlertConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertConfigNotificationChannels_NotificationChannels_Notifi~",
                        column: x => x.NotificationChannelId,
                        principalTable: "NotificationChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigNotificationChannels_NotificationChannelId",
                table: "AlertConfigNotificationChannels",
                column: "NotificationChannelId");
        }
    }
}
