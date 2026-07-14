using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestrictAlertConfigDeleteOnAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts",
                column: "AlertConfigId",
                principalTable: "AlertConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts");

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts",
                column: "AlertConfigId",
                principalTable: "AlertConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
