using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsMultiRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistoryDaysDesktop",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "HistoryDaysMobile",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "IsMultiRegion",
                table: "Checks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HistoryDaysDesktop",
                table: "Checks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HistoryDaysMobile",
                table: "Checks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMultiRegion",
                table: "Checks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
