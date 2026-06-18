using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelIsInactive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "NotificationChannels");

            migrationBuilder.AddColumn<bool>(
                name: "IsInactive",
                table: "NotificationChannels",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInactive",
                table: "NotificationChannels");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "NotificationChannels",
                type: "text",
                nullable: true);
        }
    }
}
