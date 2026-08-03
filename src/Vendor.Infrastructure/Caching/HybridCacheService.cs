using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Caching;

public class HybridCacheService(
    IMemoryCache memoryCache,
    IConnectionMultiplexer? connectionMultiplexer = null,
    ILogger<HybridCacheService>? logger = null) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
        {
            try
            {
                var db = connectionMultiplexer.GetDatabase();
                var val = await db.StringGetAsync(key);
                if (val.HasValue)
                {
                    return JsonSerializer.Deserialize<T>((string)val!);
                }
                return default;
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
            {
                logger?.LogWarning(ex, "Redis operation failed in GetAsync for key '{Key}', falling back to IMemoryCache.", key);
                Debug.WriteLine($"Redis exception in GetAsync: {ex}");
            }
        }

        if (memoryCache.TryGetValue(key, out T? cachedValue))
        {
            return cachedValue;
        }
        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var exp = expiration ?? TimeSpan.FromMinutes(10);
        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
        {
            try
            {
                var db = connectionMultiplexer.GetDatabase();
                var json = JsonSerializer.Serialize(value);
                await db.StringSetAsync(key, json, exp);
                memoryCache.Remove(key);
                return;
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
            {
                logger?.LogWarning(ex, "Redis operation failed in SetAsync for key '{Key}', falling back to IMemoryCache.", key);
                Debug.WriteLine($"Redis exception in SetAsync: {ex}");
            }
        }

        memoryCache.Set(key, value, exp);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
        {
            try
            {
                var db = connectionMultiplexer.GetDatabase();
                await db.KeyDeleteAsync(key);
                memoryCache.Remove(key);
                return;
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
            {
                logger?.LogWarning(ex, "Redis operation failed in RemoveAsync for key '{Key}', falling back to IMemoryCache.", key);
                Debug.WriteLine($"Redis exception in RemoveAsync: {ex}");
            }
        }

        memoryCache.Remove(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
        {
            try
            {
                var endpoints = connectionMultiplexer.GetEndPoints();
                if (endpoints.Length > 0)
                {
                    var server = connectionMultiplexer.GetServer(endpoints.First());
                    var keys = server.Keys(pattern: $"{prefix}*").ToArray();
                    if (keys.Length > 0)
                    {
                        var db = connectionMultiplexer.GetDatabase();
                        await db.KeyDeleteAsync(keys);
                    }
                }
                return;
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
            {
                logger?.LogWarning(ex, "Redis operation failed in RemoveByPrefixAsync for prefix '{Prefix}', falling back to IMemoryCache.", prefix);
                Debug.WriteLine($"Redis exception in RemoveByPrefixAsync: {ex}");
            }
        }

        // MemoryCache does not support native key iteration safely; fallback complete
    }
}
