using Fundoo.Notification.Domain.Enums;
namespace Fundoo.Notification.Domain.Entities;

public sealed class NotificationIdempotency
{
    public Guid Id { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}