namespace Fundoo.Common.Messaging;

public interface IKafkaProducer : IDisposable
{
    Task<bool> ProduceAsync(
        string topic,
        string key,
        string value,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}
