using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEscalationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "EscalationPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReEscalateAfterAckMinutes = table.Column<int>(type: "integer", nullable: false),
                    ReEscalateAfterInactivityMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    ScheduleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationSteps_EscalationPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "EscalationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EscalationSteps_OnCallSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "OnCallSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_EscalationPolicyId",
                table: "Incidents",
                column: "EscalationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationSteps_PolicyId_Order",
                table: "EscalationSteps",
                columns: new[] { "PolicyId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationSteps_ScheduleId",
                table: "EscalationSteps",
                column: "ScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_EscalationPolicies_EscalationPolicyId",
                table: "Incidents",
                column: "EscalationPolicyId",
                principalTable: "EscalationPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_EscalationPolicies_EscalationPolicyId",
                table: "Incidents");

            migrationBuilder.DropTable(
                name: "EscalationSteps");

            migrationBuilder.DropTable(
                name: "EscalationPolicies");

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
        }
    }
}
