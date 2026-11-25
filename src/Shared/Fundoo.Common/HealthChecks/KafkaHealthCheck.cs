using Confluent.Kafka;
using Fundoo.Common.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Fundoo.Common.HealthChecks;

public class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaSettings _settings;

    public KafkaHealthCheck(IOptions<KafkaSettings> options)
    {
        _settings = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Kafka is disabled (Kafka:Enabled=false)."));
        }

        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                SocketTimeoutMs = 3000
            };

            using var adminClient = new AdminClientBuilder(config).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(3));

            if (metadata?.Brokers?.Count > 0)
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Kafka connected. Found {metadata.Brokers.Count} broker(s)."));
            }

            return Task.FromResult(HealthCheckResult.Degraded("Kafka returned empty broker metadata."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Degraded($"Kafka unavailable: {ex.Message}"));
        }
    }
}
