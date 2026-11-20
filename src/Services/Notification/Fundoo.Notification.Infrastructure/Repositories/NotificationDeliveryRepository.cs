using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Domain.Entities;
using Fundoo.Notification.Domain.Enums;
using Fundoo.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notification.Infrastructure.Repositories;

public sealed class NotificationDeliveryRepository(NotificationDbContext context)
    : INotificationDeliveryRepository
{
    public async Task AddAsync(NotificationDelivery delivery, CancellationToken cancellationToken)
    {
        await context.NotificationDeliveries.AddAsync(delivery, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task MarkSentAsync(Guid deliveryId, DateTime sentAtUtc, CancellationToken cancellationToken) =>
        context.NotificationDeliveries
            .Where(x => x.Id == deliveryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, NotificationStatus.Sent)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.LastAttemptAtUtc, sentAtUtc)
                .SetProperty(x => x.SentAtUtc, sentAtUtc)
                .SetProperty(x => x.FailedAtUtc, (DateTime?)null)
                .SetProperty(x => x.FailureReason, (string?)null), cancellationToken);

    public Task MarkFailedAsync(
        Guid deliveryId,
        DateTime failedAtUtc,
        string failureReason,
        CancellationToken cancellationToken) =>
        context.NotificationDeliveries
            .Where(x => x.Id == deliveryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, NotificationStatus.Failed)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.LastAttemptAtUtc, failedAtUtc)
                .SetProperty(x => x.FailedAtUtc, failedAtUtc)
                .SetProperty(x => x.FailureReason, failureReason), cancellationToken);
}
