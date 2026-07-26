using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Saml2ProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Saml2ProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IdpEntityId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdpSsoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IdpSigningCertificate = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SpEntityId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AllowedDomains = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Saml2ProviderConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Saml2ProviderConfigs");
        }
    }
}
