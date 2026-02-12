using KasahQMS.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// In-memory cache service implementation.
/// Can be extended to support distributed caching (Redis) for horizontal scaling.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;

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

        _memoryCache.Set(key, value, options);
        _logger.LogDebug("Cache set for key: {Key}", key);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        _logger.LogDebug("Cache removed for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        // Note: IMemoryCache doesn't support prefix-based removal natively
        // For production, consider using a distributed cache like Redis that supports pattern-based key deletion
        // This is a limitation of the in-memory implementation
        _logger.LogWarning("RemoveByPrefixAsync is not fully supported with IMemoryCache. Consider using Redis for production.");
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

