using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Caching;

public class InMemoryCacheService(IMemoryCache memoryCache) : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        memoryCache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue) options.SetAbsoluteExpiration(expiration.Value);
        memoryCache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}

public class RedisCacheService(IDistributedCache distributedCache) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var json = await distributedCache.GetStringAsync(key, ct);
        if (string.IsNullOrEmpty(json)) return default;

        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions();
        if (expiration.HasValue) options.SetAbsoluteExpiration(expiration.Value);

        var json = JsonSerializer.Serialize(value);
        await distributedCache.SetStringAsync(key, json, options, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await distributedCache.RemoveAsync(key, ct);
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
