using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CheckRequiredWorkerTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckRequiredWorkerTags",
                columns: table => new
                {
                    CheckId = table.Column<int>(type: "integer", nullable: false),
                    TagId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckRequiredWorkerTags", x => new { x.CheckId, x.TagId });
                    table.ForeignKey(
                        name: "FK_CheckRequiredWorkerTags_Checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "Checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckRequiredWorkerTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckRequiredWorkerTags_CheckId",
                table: "CheckRequiredWorkerTags",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckRequiredWorkerTags_TagId",
                table: "CheckRequiredWorkerTags",
                column: "TagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckRequiredWorkerTags");
        }
    }
}
