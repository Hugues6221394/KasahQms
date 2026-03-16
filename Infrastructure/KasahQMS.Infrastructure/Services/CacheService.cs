using System.Collections.Concurrent;
using KasahQMS.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// In-memory cache service implementation with key tracking for prefix-based removal.
/// For horizontal scaling, consider Redis which natively supports pattern-based key deletion.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keyRegistry = new();

    public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return Task.FromResult(value);
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
        {
            options.SetAbsoluteExpiration(expiration.Value);
        }
        else
        {
            options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
        }

        // Set sliding expiration to extend cache on access
        options.SetSlidingExpiration(TimeSpan.FromMinutes(5));

        // Register callback to remove key from registry when evicted
        options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            _keyRegistry.TryRemove(evictedKey.ToString()!, out _);
        });

        _memoryCache.Set(key, value, options);
        _keyRegistry.TryAdd(key, 0);
        _logger.LogDebug("Cache set for key: {Key}", key);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        _keyRegistry.TryRemove(key, out _);
        _logger.LogDebug("Cache removed for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _keyRegistry.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _keyRegistry.TryRemove(key, out _);
        }

        _logger.LogDebug("Cache removed {Count} keys with prefix: {Prefix}", keysToRemove.Count, prefix);
        return Task.CompletedTask;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var value = await GetAsync<T>(key, cancellationToken);
        if (value != null)
        {
            return value;
        }

        try
        {
            value = await factory();
            if (value != null)
            {
                await SetAsync(key, value, expiration, cancellationToken);
            }
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create cache entry for key: {Key}", key);
            throw;
        }
    }
}
