using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventsJson = table.Column<string>(type: "text", nullable: false),
                    MinSeverity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Target = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationSubscriptions_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_Enabled",
                table: "NotificationSubscriptions",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_IntegrationId",
                table: "NotificationSubscriptions",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_UserId",
                table: "NotificationSubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationSubscriptions");
        }
    }
}
