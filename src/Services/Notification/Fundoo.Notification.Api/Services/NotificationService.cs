using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Domain.Entities;
using Fundoo.Notification.Domain.Enums;

namespace Fundoo.Notification.Api.Services;

public sealed class NotificationService(
    INotificationIdempotencyRepository repository,
    INotificationDeliveryRepository deliveryRepository,
    IEmailSender emailSender) : INotificationService
{
    public async Task SendPasswordResetNotificationAsync(
        PasswordResetNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var isNewRequest = await repository.TryCreatePendingAsync(
            request.IdempotencyKey,
            cancellationToken);

        if (!isNewRequest)
        {
            var existingStatus = await repository.GetStatusAsync(
                request.IdempotencyKey,
                cancellationToken);

            if (existingStatus is NotificationStatus.Pending
                or NotificationStatus.Sent)
            {
                return;
            }

            throw new InvalidOperationException(
                "This notification key has already been processed unsuccessfully.");
        }

        var delivery = CreateDelivery(
            request.IdempotencyKey,
            NotificationType.PasswordReset,
            request.RecipientEmail,
            request.RecipientName);

        await deliveryRepository.AddAsync(delivery, cancellationToken);

        try
        {
            await emailSender.SendPasswordResetNotificationAsync(
                request,
                cancellationToken);

            await repository.MarkSentAsync(
                request.IdempotencyKey,
                cancellationToken);
            await deliveryRepository.MarkSentAsync(
                delivery.Id,
                DateTime.UtcNow,
                cancellationToken);
        }
        catch (Exception exception)
        {
            await deliveryRepository.MarkFailedAsync(
                delivery.Id,
                DateTime.UtcNow,
                SanitizeFailureReason(exception),
                CancellationToken.None);
            await repository.MarkFailedAsync(
                request.IdempotencyKey,
                CancellationToken.None);

            throw;
        }
    }

    public async Task SendWelcomeNotificationAsync(
        WelcomeNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var isNewRequest = await repository.TryCreatePendingAsync(
                request.IdempotencyKey,
                cancellationToken);

            if (!isNewRequest)
            {
                var existingStatus = await repository.GetStatusAsync(
                    request.IdempotencyKey,
                    cancellationToken);

                if (existingStatus is NotificationStatus.Pending or NotificationStatus.Sent)
                {
                    return;
                }
            }
        }

        var delivery = CreateDelivery(
            request.IdempotencyKey,
            NotificationType.Welcome,
            request.RecipientEmail,
            request.RecipientName);

        await deliveryRepository.AddAsync(delivery, cancellationToken);

        try
        {
            await emailSender.SendWelcomeNotificationAsync(request, cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                await repository.MarkSentAsync(request.IdempotencyKey, cancellationToken);
            }

            await deliveryRepository.MarkSentAsync(
                delivery.Id,
                DateTime.UtcNow,
                cancellationToken);
        }
        catch (Exception exception)
        {
            await deliveryRepository.MarkFailedAsync(
                delivery.Id,
                DateTime.UtcNow,
                SanitizeFailureReason(exception),
                CancellationToken.None);

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                await repository.MarkFailedAsync(request.IdempotencyKey, CancellationToken.None);
            }

            throw;
        }
    }

    private static NotificationDelivery CreateDelivery(
        string? idempotencyKey,
        NotificationType type,
        string recipientEmail,
        string? recipientName) => new()
    {
        Id = Guid.NewGuid(),
        IdempotencyKey = idempotencyKey,
        Type = type,
        RecipientEmail = recipientEmail,
        RecipientName = recipientName,
        Status = NotificationStatus.Pending,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static string SanitizeFailureReason(Exception exception) =>
        exception.GetType().Name.Length <= 2000
            ? exception.GetType().Name
            : exception.GetType().Name[..2000];
}
