namespace Fundoo.Common.Messaging.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
    public string? CorrelationId { get; init; }
}
