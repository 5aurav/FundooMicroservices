namespace Fundoo.Common.Messaging;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ClientId { get; set; } = "fundoo-service";
    public string ConsumerGroupId { get; set; } = "fundoo-group";
    public int RetryCount { get; set; } = 3;
    public int RetryBackoffMs { get; set; } = 1000;
}
