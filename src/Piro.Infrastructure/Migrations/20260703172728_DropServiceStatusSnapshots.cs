using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropServiceStatusSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceStatusSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceStatusSnapshots",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    ComputedStatus = table.Column<string>(type: "text", nullable: false),
                    PropagationSources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStatusSnapshots", x => new { x.ServiceId, x.Timestamp });
                    table.ForeignKey(
                        name: "FK_ServiceStatusSnapshots_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStatusSnapshots_ServiceId_Timestamp",
                table: "ServiceStatusSnapshots",
                columns: new[] { "ServiceId", "Timestamp" });
        }
    }
}
