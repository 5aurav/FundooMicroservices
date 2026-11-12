using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Domain.Enums;

namespace Fundoo.Notification.Api.Services;

public sealed class NotificationService(
    INotificationIdempotencyRepository repository,
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

        try
        {
            await emailSender.SendPasswordResetNotificationAsync(
                request,
                cancellationToken);

            await repository.MarkSentAsync(
                request.IdempotencyKey,
                cancellationToken);
        }
        catch
        {
            await repository.MarkFailedAsync(
                request.IdempotencyKey,
                CancellationToken.None);

            throw;
        }
    }

    public Task SendWelcomeNotificationAsync(
        WelcomeNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        return emailSender.SendWelcomeNotificationAsync(
            request,
            cancellationToken);
    }
}