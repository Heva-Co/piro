using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostmortems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostmortemFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Heading = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HelpText = table.Column<string>(type: "text", nullable: true),
                    FieldType = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostmortemFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Postmortems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewOwnerUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewOwnerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImpactStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImpactEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postmortems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Postmortems_AspNetUsers_ReviewOwnerUserId",
                        column: x => x.ReviewOwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PostmortemFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostmortemId = table.Column<int>(type: "integer", nullable: false),
                    FieldDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostmortemFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostmortemFieldValues_PostmortemFieldDefinitions_FieldDefin~",
                        column: x => x.FieldDefinitionId,
                        principalTable: "PostmortemFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostmortemFieldValues_Postmortems_PostmortemId",
                        column: x => x.PostmortemId,
                        principalTable: "Postmortems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostmortemIncidents",
                columns: table => new
                {
                    PostmortemId = table.Column<int>(type: "integer", nullable: false),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostmortemIncidents", x => new { x.PostmortemId, x.IncidentId });
                    table.ForeignKey(
                        name: "FK_PostmortemIncidents_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostmortemIncidents_Postmortems_PostmortemId",
                        column: x => x.PostmortemId,
                        principalTable: "Postmortems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PostmortemFieldDefinitions",
                columns: new[] { "Id", "FieldType", "Heading", "HelpText", "IsActive", "IsSystem", "Key", "SortOrder" },
                values: new object[,]
                {
                    { 1, "LongText", "Overview", "A high-level summary of what happened, for a general audience.", true, true, "overview", 0 },
                    { 2, "LongText", "What Happened", "A detailed, chronological account of the incident.", true, true, "what_happened", 1 },
                    { 3, "LongText", "Resolution", "How the incident was ultimately resolved.", true, true, "resolution", 2 },
                    { 4, "LongText", "Root Causes", "The conditions that allowed the incident to happen. Aim for the underlying causes, not just the trigger.", true, true, "root_causes", 3 },
                    { 5, "LongText", "Impact", "Who and what was affected, and to what degree.", true, true, "impact", 4 },
                    { 6, "LongText", "What Went Well?", "Things that worked as intended during detection and response.", true, true, "what_went_well", 5 },
                    { 7, "LongText", "What Didn't Go So Well?", "Things that hindered detection or response and should be improved.", true, true, "what_didnt", 6 },
                    { 8, "LongText", "Action Items", "Concrete follow-up work, each with an accountable owner.", true, true, "action_items", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemFieldDefinitions_Key",
                table: "PostmortemFieldDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemFieldValues_FieldDefinitionId",
                table: "PostmortemFieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemFieldValues_PostmortemId_FieldDefinitionId",
                table: "PostmortemFieldValues",
                columns: new[] { "PostmortemId", "FieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemIncidents_IncidentId",
                table: "PostmortemIncidents",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Postmortems_ReviewOwnerUserId",
                table: "Postmortems",
                column: "ReviewOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Postmortems_Status",
                table: "Postmortems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostmortemFieldValues");

            migrationBuilder.DropTable(
                name: "PostmortemIncidents");

            migrationBuilder.DropTable(
                name: "PostmortemFieldDefinitions");

            migrationBuilder.DropTable(
                name: "Postmortems");
        }
    }
}
