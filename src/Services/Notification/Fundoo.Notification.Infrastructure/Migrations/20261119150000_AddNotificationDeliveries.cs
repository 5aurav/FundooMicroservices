using System;
using Fundoo.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fundoo.Notification.Infrastructure.Migrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20261119150000_AddNotificationDeliveries")]
public partial class AddNotificationDeliveries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Type = table.Column<int>(type: "integer", nullable: false),
                RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_NotificationDeliveries", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_NotificationDeliveries_IdempotencyKey",
            table: "NotificationDeliveries",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NotificationDeliveries_Status_CreatedAtUtc",
            table: "NotificationDeliveries",
            columns: new[] { "Status", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NotificationDeliveries");
    }
}
