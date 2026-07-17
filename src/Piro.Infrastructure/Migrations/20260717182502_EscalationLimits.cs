using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EscalationLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "EscalationSteps",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "RetryIntervalMinutes",
                table: "EscalationSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalationExhaustedAt",
                table: "Alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalationStepAttempts",
                table: "Alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "EscalationSteps");

            migrationBuilder.DropColumn(
                name: "RetryIntervalMinutes",
                table: "EscalationSteps");

            migrationBuilder.DropColumn(
                name: "EscalationExhaustedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "EscalationStepAttempts",
                table: "Alerts");
        }
    }
}
