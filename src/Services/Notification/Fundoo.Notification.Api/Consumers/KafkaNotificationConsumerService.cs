using Confluent.Kafka;
using Fundoo.Common.Messaging;
using Fundoo.Common.Messaging.Events;
using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Fundoo.Notification.Api.Consumers;

public class KafkaNotificationConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKafkaProducer _dlqProducer;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaNotificationConsumerService> _logger;

    public KafkaNotificationConsumerService(
        IServiceScopeFactory scopeFactory,
        IKafkaProducer dlqProducer,
        IOptions<KafkaSettings> options,
        ILogger<KafkaNotificationConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _dlqProducer = dlqProducer;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Ensure background service starts without blocking bootstrap

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = string.IsNullOrWhiteSpace(_settings.ConsumerGroupId)
                ? "fundoo-notification-consumer"
                : _settings.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        try
        {
            consumer.Subscribe(KafkaTopics.UserEvents);
            _logger.LogInformation("KafkaNotificationConsumerService subscribed to {Topic}", KafkaTopics.UserEvents);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (consumeResult == null || consumeResult.IsPartitionEOF)
                    {
                        continue;
                    }

                    var success = await ProcessMessageWithRetryAsync(consumeResult, stoppingToken);

                    if (success)
                    {
                        consumer.Commit(consumeResult);
                    }
                    else
                    {
                        // Exceeded retries - send to DLQ and commit offset to avoid poison pill deadlock
                        await RouteToDeadLetterQueueAsync(consumeResult, stoppingToken);
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("KafkaNotificationConsumerService cancellation requested.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in KafkaNotificationConsumerService.");
        }
        finally
        {
            try
            {
                consumer.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing Kafka consumer.");
            }
        }
    }

    private async Task<bool> ProcessMessageWithRetryAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken stoppingToken)
    {
        const int maxAttempts = 3;
        var attempt = 0;

        while (attempt < maxAttempts && !stoppingToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                await HandleEventAsync(consumeResult, stoppingToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxAttempts} failed for message at topic {Topic}, partition {Partition}, offset {Offset}",
                    attempt, maxAttempts, consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value);

                if (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, stoppingToken);
                }
            }
        }

        return false;
    }

    private async Task HandleEventAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken stoppingToken)
    {
        var rawPayload = consumeResult.Message.Value;
        var eventType = GetHeaderValue(consumeResult.Message.Headers, "EventType");

        // Infer from payload structure if header is missing
        if (string.IsNullOrWhiteSpace(eventType))
        {
            using var doc = JsonDocument.Parse(rawPayload);
            if (doc.RootElement.TryGetProperty("EventType", out var typeProp))
            {
                eventType = typeProp.GetString();
            }
            else if (doc.RootElement.TryGetProperty("ResetToken", out _))
            {
                eventType = nameof(PasswordResetRequestedEvent);
            }
            else if (doc.RootElement.TryGetProperty("Email", out _))
            {
                eventType = nameof(UserRegisteredEvent);
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        switch (eventType)
        {
            case nameof(UserRegisteredEvent):
            {
                var evt = JsonSerializer.Deserialize<UserRegisteredEvent>(rawPayload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Failed to deserialize UserRegisteredEvent");

                var welcomeRequest = new WelcomeNotificationRequest
                {
                    IdempotencyKey = $"welcome-{evt.UserId}",
                    RecipientEmail = evt.Email,
                    RecipientName = $"{evt.FirstName} {evt.LastName}".Trim()
                };

                await notificationService.SendWelcomeNotificationAsync(welcomeRequest, stoppingToken);
                _logger.LogInformation("Processed UserRegisteredEvent for UserId {UserId}", evt.UserId);
                break;
            }

            case nameof(PasswordResetRequestedEvent):
            {
                var evt = JsonSerializer.Deserialize<PasswordResetRequestedEvent>(rawPayload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Failed to deserialize PasswordResetRequestedEvent");

                var resetRequest = new PasswordResetNotificationRequest
                {
                    IdempotencyKey = evt.ResetTokenId.ToString(),
                    RecipientEmail = evt.RecipientEmail,
                    RecipientName = evt.RecipientName,
                    ResetToken = evt.ResetToken
                };

                await notificationService.SendPasswordResetNotificationAsync(resetRequest, stoppingToken);
                _logger.LogInformation("Processed PasswordResetRequestedEvent for ResetTokenId {ResetTokenId}", evt.ResetTokenId);
                break;
            }

            default:
                _logger.LogWarning("Unknown or unhandled event type '{EventType}' on topic {Topic}", eventType, consumeResult.Topic);
                break;
        }
    }

    private async Task RouteToDeadLetterQueueAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken stoppingToken)
    {
        var headers = new Dictionary<string, string>
        {
            { "x-original-topic", consumeResult.Topic },
            { "x-original-partition", consumeResult.Partition.Value.ToString() },
            { "x-original-offset", consumeResult.Offset.Value.ToString() },
            { "x-failed-at-utc", DateTime.UtcNow.ToString("O") },
            { "x-consumer-group", _settings.ConsumerGroupId }
        };

        var key = consumeResult.Message.Key ?? Guid.NewGuid().ToString();
        var value = consumeResult.Message.Value ?? string.Empty;

        _logger.LogWarning("Routing poisoned message (Key: {Key}) to Dead-Letter Queue topic {DlqTopic}",
            key, KafkaTopics.NotificationDlq);

        await _dlqProducer.ProduceAsync(
            KafkaTopics.NotificationDlq,
            key,
            value,
            headers,
            stoppingToken);
    }

    private static string? GetHeaderValue(Headers? headers, string key)
    {
        if (headers == null) return null;
        if (headers.TryGetLastBytes(key, out var bytes) && bytes != null)
        {
            return Encoding.UTF8.GetString(bytes);
        }
        return null;
    }
}
