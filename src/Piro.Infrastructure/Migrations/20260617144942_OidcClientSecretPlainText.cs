using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OidcClientSecretPlainText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientSecretProtected",
                table: "OidcProviderConfigs");

            migrationBuilder.AlterColumn<string>(
                name: "RedirectUri",
                table: "OidcProviderConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "ClientSecret",
                table: "OidcProviderConfigs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientSecret",
                table: "OidcProviderConfigs");

            migrationBuilder.AlterColumn<string>(
                name: "RedirectUri",
                table: "OidcProviderConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ClientSecretProtected",
                table: "OidcProviderConfigs",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
