using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeyLastUsedAtAndStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ApiKeys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "ACTIVE");

            // Existing rows store the old plain-string values ("ACTIVE"/"REVOKED"); the enum
            // conversion serializes C# enum member names ("Active"/"Revoked") instead.
            migrationBuilder.Sql("UPDATE \"ApiKeys\" SET \"Status\" = 'Active' WHERE \"Status\" = 'ACTIVE';");
            migrationBuilder.Sql("UPDATE \"ApiKeys\" SET \"Status\" = 'Revoked' WHERE \"Status\" = 'REVOKED';");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "ApiKeys",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "ApiKeys");

            migrationBuilder.Sql("UPDATE \"ApiKeys\" SET \"Status\" = 'ACTIVE' WHERE \"Status\" = 'Active';");
            migrationBuilder.Sql("UPDATE \"ApiKeys\" SET \"Status\" = 'REVOKED' WHERE \"Status\" = 'Revoked';");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ApiKeys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ACTIVE",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Active");
        }
    }
}
