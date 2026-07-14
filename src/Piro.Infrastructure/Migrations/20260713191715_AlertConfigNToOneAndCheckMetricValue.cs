using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlertConfigNToOneAndCheckMetricValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs");

            migrationBuilder.DropColumn(
                name: "FailureThreshold",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "RecoveryThreshold",
                table: "Checks");

            migrationBuilder.AddColumn<double>(
                name: "MetricValue",
                table: "CheckDataPoints",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs",
                column: "CheckId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs");

            migrationBuilder.DropColumn(
                name: "MetricValue",
                table: "CheckDataPoints");

            migrationBuilder.AddColumn<int>(
                name: "FailureThreshold",
                table: "Checks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecoveryThreshold",
                table: "Checks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs",
                column: "CheckId",
                unique: true);
        }
    }
}
