using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Caching;

public class HybridCacheService(IMemoryCache memoryCache, IConnectionMultiplexer? connectionMultiplexer = null) : ICacheService
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
            catch
            {
                // Fall back to MemoryCache on Redis exception
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
                return;
            }
            catch
            {
                // Fall back to MemoryCache on Redis exception
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
                return;
            }
            catch
            {
                // Fall back to MemoryCache on Redis exception
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
            catch
            {
                // Fall back gracefully if Redis server key search fails
            }
        }

        // MemoryCache does not support native key iteration safely; fallback complete
    }
}
