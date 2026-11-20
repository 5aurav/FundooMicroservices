using Fundoo.Notification.Domain.Entities;

namespace Fundoo.Notification.Application.Abstractions;

public interface INotificationDeliveryRepository
{
    Task AddAsync(NotificationDelivery delivery, CancellationToken cancellationToken);
    Task MarkSentAsync(Guid deliveryId, DateTime sentAtUtc, CancellationToken cancellationToken);
    Task MarkFailedAsync(Guid deliveryId, DateTime failedAtUtc, string failureReason, CancellationToken cancellationToken);
}
