using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReEscalateAfterAck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReEscalateAfterAckMinutes",
                table: "EscalationPolicies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReEscalateAfterAckMinutes",
                table: "EscalationPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
