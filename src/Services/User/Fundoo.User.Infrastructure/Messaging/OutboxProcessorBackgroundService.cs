using Fundoo.Common.Messaging;
using Fundoo.User.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fundoo.User.Infrastructure.Messaging;

public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(3);

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IKafkaProducer kafkaProducer,
        ILogger<OutboxProcessorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessBatchAsync(stoppingToken);

                if (processedCount == 0)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages.");
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxProcessorBackgroundService is stopping.");
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 10)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            var headers = new Dictionary<string, string>
            {
                { "EventType", message.EventType },
                { "CorrelationId", message.CorrelationId ?? message.Id.ToString() }
            };

            var published = await _kafkaProducer.ProduceAsync(
                KafkaTopics.UserEvents,
                message.CorrelationId ?? message.Id.ToString(),
                message.Payload,
                headers,
                cancellationToken);

            if (published)
            {
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.Error = null;
                _logger.LogInformation("Dispatched outbox event {EventType} (Id: {Id})", message.EventType, message.Id);
            }
            else
            {
                message.RetryCount++;
                message.Error = $"Failed to dispatch to Kafka at {DateTime.UtcNow}";
                _logger.LogWarning("Failed to dispatch outbox event {EventType} (Id: {Id}), retry count: {RetryCount}",
                    message.EventType, message.Id, message.RetryCount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}
