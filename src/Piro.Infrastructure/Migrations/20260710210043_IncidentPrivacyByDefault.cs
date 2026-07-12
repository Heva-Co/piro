using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IncidentPrivacyByDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Incidents");

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "Incidents",
                type: "text",
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "IncidentComments",
                type: "text",
                nullable: false,
                defaultValue: "Private");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "IncidentComments");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Incidents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
