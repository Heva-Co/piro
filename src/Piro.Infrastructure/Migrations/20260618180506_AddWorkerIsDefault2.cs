using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerIsDefault2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Status" was already dropped manually; add IsInactive idempotently.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'NotificationChannels' AND column_name = 'IsInactive'
                    ) THEN
                        ALTER TABLE ""NotificationChannels"" ADD COLUMN ""IsInactive"" boolean NOT NULL DEFAULT false;
                    END IF;
                END
                $$;
            ");
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
