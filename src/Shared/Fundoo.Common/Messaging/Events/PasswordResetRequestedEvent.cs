namespace Fundoo.Common.Messaging.Events;

public sealed record PasswordResetRequestedEvent : IntegrationEvent
{
    public Guid ResetTokenId { get; init; }
    public Guid UserId { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public string ResetToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}
