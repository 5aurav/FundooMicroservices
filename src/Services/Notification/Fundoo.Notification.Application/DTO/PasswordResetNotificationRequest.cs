namespace Fundoo.Notification.Application.DTO
{
    public sealed class PasswordResetNotificationRequest
    {
        public string IdempotencyKey { get; set; } = string.Empty;

        public string RecipientEmail { get; set; } = string.Empty;

        public string RecipientName { get; set; } = string.Empty;

        public string ResetToken { get; set; } = string.Empty;
    }
}