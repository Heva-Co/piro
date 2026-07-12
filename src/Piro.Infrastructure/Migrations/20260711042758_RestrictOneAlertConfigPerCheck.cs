using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestrictOneAlertConfigPerCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration: restricted to one AlertConfig per Check going forward. Any check
            // with more than one existing AlertConfig keeps only its most recently updated one
            // (FK cascade on Alerts.AlertConfigId removes the history tied to the deleted configs).
            migrationBuilder.Sql(@"
                DELETE FROM ""AlertConfigs"" ac
                USING (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (PARTITION BY ""CheckId"" ORDER BY ""UpdatedAt"" DESC, ""Id"" DESC) AS rn
                    FROM ""AlertConfigs""
                ) ranked
                WHERE ac.""Id"" = ranked.""Id"" AND ranked.rn > 1;
            ");

            migrationBuilder.DropIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs");

            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs",
                column: "CheckId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs");

            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs",
                column: "CheckId");
        }
    }
}
