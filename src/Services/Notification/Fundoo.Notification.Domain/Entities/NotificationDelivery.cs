using Fundoo.Notification.Domain.Enums;

namespace Fundoo.Notification.Domain.Entities;

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }
    public string? IdempotencyKey { get; set; }
    public NotificationType Type { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public NotificationStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? FailureReason { get; set; }
}
