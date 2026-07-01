using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntegrationId",
                table: "Checks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Checks_IntegrationId",
                table: "Checks",
                column: "IntegrationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Checks_Integrations_IntegrationId",
                table: "Checks",
                column: "IntegrationId",
                principalTable: "Integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Checks_Integrations_IntegrationId",
                table: "Checks");

            migrationBuilder.DropTable(
                name: "Integrations");

            migrationBuilder.DropIndex(
                name: "IX_Checks_IntegrationId",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "IntegrationId",
                table: "Checks");
        }
    }
}
