using Microsoft.Extensions.Caching.Memory;
using Moq;
using StackExchange.Redis;
using Vendor.Application.Common.Interfaces;
using Vendor.Infrastructure.Caching;
using Xunit;

namespace Vendor.Infrastructure.Tests.Caching;

public class HybridCacheServiceTests
{
    [Fact]
    public async Task SetAsync_And_GetAsync_MemoryCacheFallback_WhenRedisIsNull_Works()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheService = new HybridCacheService(memoryCache, connectionMultiplexer: null);
        var key = "test_key_null_redis";
        var value = "hello_null_redis";

        // Act
        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var cached = await cacheService.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, cached);
    }

    [Fact]
    public async Task SetAsync_And_GetAsync_MemoryCacheFallback_WhenRedisIsDisconnected_Works()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.IsConnected).Returns(false);

        var cacheService = new HybridCacheService(memoryCache, redisMock.Object);
        var key = "test_key_disconnected_redis";
        var value = "hello_disconnected";

        // Act
        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var cached = await cacheService.GetAsync<string>(key);

        // Assert
        Assert.Equal(value, cached);
    }

    [Fact]
    public async Task RemoveAsync_MemoryCacheFallback_WhenRedisIsNull_RemovesValue()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheService = new HybridCacheService(memoryCache, connectionMultiplexer: null);
        var key = "test_key_remove";
        var value = "value_to_remove";

        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var initialGet = await cacheService.GetAsync<string>(key);
        Assert.Equal(value, initialGet);

        // Act
        await cacheService.RemoveAsync(key);
        var afterRemove = await cacheService.GetAsync<string>(key);

        // Assert
        Assert.Null(afterRemove);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_MemoryCacheFallback_WhenRedisIsNull_DoesNotThrow()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheService = new HybridCacheService(memoryCache, connectionMultiplexer: null);

        // Act & Assert (should complete without throwing)
        await cacheService.RemoveByPrefixAsync("prefix_test_");
    }
}
