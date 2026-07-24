using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeyHeartbeatScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckId",
                table: "ApiKeys",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "ApiKeys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Full");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_CheckId",
                table: "ApiKeys",
                column: "CheckId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Checks_CheckId",
                table: "ApiKeys",
                column: "CheckId",
                principalTable: "Checks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_Checks_CheckId",
                table: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_CheckId",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "CheckId",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "ApiKeys");
        }
    }
}
