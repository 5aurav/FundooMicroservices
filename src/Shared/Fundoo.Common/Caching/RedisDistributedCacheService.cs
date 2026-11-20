using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fundoo.Common.Caching;

public class RedisDistributedCacheService : IRedisDistributedCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisDistributedCacheService> _logger;

    public RedisDistributedCacheService(
        IDistributedCache cache,
        ILogger<RedisDistributedCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await _cache.GetStringAsync(key, cancellationToken);
            if (string.IsNullOrEmpty(data)) return default;
            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cache key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30)
            };
            await _cache.SetStringAsync(key, json, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set cache key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache key {Key}", key);
        }
    }

    public async Task<bool> CheckRateLimitAsync(
        string clientKey,
        int maxRequests,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"ratelimit:{clientKey}";
        try
        {
            var countStr = await _cache.GetStringAsync(cacheKey, cancellationToken);
            var currentCount = 0;
            if (!string.IsNullOrEmpty(countStr) && int.TryParse(countStr, out var parsed))
            {
                currentCount = parsed;
            }

            if (currentCount >= maxRequests)
            {
                return false; // Rate limit exceeded
            }

            currentCount++;
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = window
            };
            await _cache.SetStringAsync(cacheKey, currentCount.ToString(), options, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rate limit check failed for client key {ClientKey}, failing open", clientKey);
            return true; // Fail open on cache error to preserve service availability
        }
    }
}
