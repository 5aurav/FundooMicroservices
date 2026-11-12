using Fundoo.Notification.Application.DTO;

namespace Fundoo.Notification.Application.Abstractions;

public interface IEmailSender
{
    Task SendPasswordResetNotificationAsync(
        PasswordResetNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task SendWelcomeNotificationAsync(
        WelcomeNotificationRequest request,
        CancellationToken cancellationToken = default);
}