using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Fundoo.Common.Messaging;

public class KafkaProducer : IKafkaProducer
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private bool _disposed;

    public KafkaProducer(IOptions<KafkaSettings> options, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var settings = options.Value;

        var config = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            ClientId = settings.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = settings.RetryCount,
            RetryBackoffMs = settings.RetryBackoffMs
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task<bool> ProduceAsync(
        string topic,
        string key,
        string value,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = key,
                Value = value,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };

            if (headers != null && headers.Count > 0)
            {
                message.Headers = new Headers();
                foreach (var (headerKey, headerValue) in headers)
                {
                    message.Headers.Add(headerKey, Encoding.UTF8.GetBytes(headerValue));
                }
            }

            var deliveryResult = await _producer.ProduceAsync(topic, message, cancellationToken);
            _logger.LogInformation(
                "Kafka message produced to topic {Topic}, partition {Partition}, offset {Offset}",
                deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);

            return deliveryResult.Status == PersistenceStatus.Persisted;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Kafka produce failure on topic {Topic} for key {Key}: {Reason}", topic, key, ex.Error.Reason);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error producing Kafka message on topic {Topic} for key {Key}", topic, key);
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
