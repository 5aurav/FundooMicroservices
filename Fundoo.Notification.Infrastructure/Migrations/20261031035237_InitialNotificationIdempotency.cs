using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fundoo.Notification.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotificationIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationIdempotencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationIdempotencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationIdempotencies_IdempotencyKey",
                table: "NotificationIdempotencies",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationIdempotencies");
        }
    }
}
