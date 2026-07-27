using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropServiceHistoryDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistoryDaysDesktop",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "HistoryDaysMobile",
                table: "Services");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HistoryDaysDesktop",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "HistoryDaysMobile",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 15);
        }
    }
}
