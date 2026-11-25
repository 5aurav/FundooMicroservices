namespace Fundoo.Common.Messaging;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    /// <summary>
    /// Set to false to disable Kafka. All producers become no-ops and
    /// consumers exit immediately. Useful for local development without a broker.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ClientId { get; set; } = "fundoo-service";
    public string ConsumerGroupId { get; set; } = "fundoo-group";
    public int RetryCount { get; set; } = 3;
    public int RetryBackoffMs { get; set; } = 1000;
}
