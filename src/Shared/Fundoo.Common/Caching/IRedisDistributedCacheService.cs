namespace Fundoo.Common.Caching;

public interface IRedisDistributedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> CheckRateLimitAsync(string clientKey, int maxRequests, TimeSpan window, CancellationToken cancellationToken = default);
}
