using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateIncidentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "State",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "State",
                table: "IncidentComments");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Incidents",
                type: "text",
                nullable: false,
                defaultValue: "Investigating",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IncidentComments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Incidents",
                type: "text",
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Investigating");

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Incidents",
                type: "text",
                nullable: false,
                defaultValue: "Investigating");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IncidentComments",
                type: "text",
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "IncidentComments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
