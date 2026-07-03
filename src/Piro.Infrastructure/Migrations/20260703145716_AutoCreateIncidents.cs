using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AutoCreateIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TriggeringCheckId",
                table: "IncidentServices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Incidents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AutomaticallyCloseIncident",
                table: "Checks",
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

            migrationBuilder.CreateIndex(
                name: "IX_IncidentServices_TriggeringCheckId",
                table: "IncidentServices",
                column: "TriggeringCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentMerges_SourceIncidentId",
                table: "IncidentMerges",
                column: "SourceIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentMerges_TargetIncidentId",
                table: "IncidentMerges",
                column: "TargetIncidentId");

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentServices_Checks_TriggeringCheckId",
                table: "IncidentServices",
                column: "TriggeringCheckId",
                principalTable: "Checks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IncidentServices_Checks_TriggeringCheckId",
                table: "IncidentServices");

            migrationBuilder.DropTable(
                name: "IncidentMerges");

            migrationBuilder.DropIndex(
                name: "IX_IncidentServices_TriggeringCheckId",
                table: "IncidentServices");

            migrationBuilder.DropColumn(
                name: "TriggeringCheckId",
                table: "IncidentServices");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "AutomaticallyCloseIncident",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "AutomaticallyCreateIncident",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "Criticality",
                table: "Checks");
        }
    }
}
