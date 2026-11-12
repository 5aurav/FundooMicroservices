using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Domain.Entities;
using Fundoo.Notification.Domain.Enums;
using Fundoo.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fundoo.Notification.Infrastructure.Repositories;

public sealed class NotificationIdempotencyRepository(
    NotificationDbContext context) : INotificationIdempotencyRepository
{
    public async Task<bool> TryCreatePendingAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var record = new NotificationIdempotency
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey,
            Status = NotificationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.NotificationIdempotencies.Add(record);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            context.Entry(record).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<NotificationStatus?> GetStatusAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await context.NotificationIdempotencies
            .Where(x => x.IdempotencyKey == idempotencyKey)
            .Select(x => (NotificationStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task MarkSentAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(
            idempotencyKey,
            NotificationStatus.Sent,
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(
            idempotencyKey,
            NotificationStatus.Failed,
            cancellationToken);
    }

    private async Task UpdateStatusAsync(
        string idempotencyKey,
        NotificationStatus status,
        CancellationToken cancellationToken)
    {
        var record = await context.NotificationIdempotencies
            .FirstOrDefaultAsync(
                x => x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (record is null)
            throw new InvalidOperationException(
                "Idempotency record was not found.");

        record.Status = status;

        if (status == NotificationStatus.Sent)
            record.ProcessedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }
}