using Fundoo.Notification.Domain.Enums;

namespace Fundoo.Notification.Application.Abstractions;

public interface INotificationIdempotencyRepository
{
    Task<bool> TryCreatePendingAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<NotificationStatus?> GetStatusAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task MarkSentAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
}