using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GcpWebhookIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts");

            migrationBuilder.AlterColumn<int>(
                name: "AlertConfigId",
                table: "Alerts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Alerts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Source_ExternalId",
                table: "Alerts",
                columns: new[] { "Source", "ExternalId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts",
                column: "AlertConfigId",
                principalTable: "AlertConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_Source_ExternalId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Alerts");

            migrationBuilder.AlterColumn<int>(
                name: "AlertConfigId",
                table: "Alerts",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_AlertConfigs_AlertConfigId",
                table: "Alerts",
                column: "AlertConfigId",
                principalTable: "AlertConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
