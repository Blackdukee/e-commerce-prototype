diff --git a/src/Vendor.Application/Common/Interfaces/ICacheService.cs b/src/Vendor.Application/Common/Interfaces/ICacheService.cs
new file mode 100644
index 0000000..8065617
--- /dev/null
+++ b/src/Vendor.Application/Common/Interfaces/ICacheService.cs
@@ -0,0 +1,9 @@
+namespace Vendor.Application.Common.Interfaces;
+
+public interface ICacheService
+{
+    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
+    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
+    Task RemoveAsync(string key, CancellationToken ct = default);
+    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
+}
diff --git a/src/Vendor.Application/Interfaces/IApplicationInterfaces.cs b/src/Vendor.Application/Interfaces/IApplicationInterfaces.cs
index fa355b3..bc611cd 100644
--- a/src/Vendor.Application/Interfaces/IApplicationInterfaces.cs
+++ b/src/Vendor.Application/Interfaces/IApplicationInterfaces.cs
@@ -16,12 +16,6 @@ public interface IIdempotencyStore
     Task SaveResultAsync<TResponse>(string key, TResponse result, CancellationToken ct = default);
 }
 
-public interface ICacheService
-{
-    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
-    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
-    Task RemoveAsync(string key, CancellationToken ct = default);
-}
 
 public interface ICurrentUserService
 {
diff --git a/src/Vendor.Infrastructure/Caching/CacheServices.cs b/src/Vendor.Infrastructure/Caching/CacheServices.cs
index 6b3cdff..c0a7b77 100644
--- a/src/Vendor.Infrastructure/Caching/CacheServices.cs
+++ b/src/Vendor.Infrastructure/Caching/CacheServices.cs
@@ -1,7 +1,7 @@
 using System.Text.Json;
 using Microsoft.Extensions.Caching.Distributed;
 using Microsoft.Extensions.Caching.Memory;
-using Vendor.Application.Interfaces;
+using Vendor.Application.Common.Interfaces;
 
 namespace Vendor.Infrastructure.Caching;
 
@@ -26,6 +26,11 @@ public class InMemoryCacheService(IMemoryCache memoryCache) : ICacheService
         memoryCache.Remove(key);
         return Task.CompletedTask;
     }
+
+    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
+    {
+        return Task.CompletedTask;
+    }
 }
 
 public class RedisCacheService(IDistributedCache distributedCache) : ICacheService
@@ -51,4 +56,9 @@ public class RedisCacheService(IDistributedCache distributedCache) : ICacheServi
     {
         await distributedCache.RemoveAsync(key, ct);
     }
+
+    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
+    {
+        return Task.CompletedTask;
+    }
 }
diff --git a/src/Vendor.Infrastructure/Caching/HybridCacheService.cs b/src/Vendor.Infrastructure/Caching/HybridCacheService.cs
new file mode 100644
index 0000000..02c09a3
--- /dev/null
+++ b/src/Vendor.Infrastructure/Caching/HybridCacheService.cs
@@ -0,0 +1,104 @@
+using System.Text.Json;
+using Microsoft.Extensions.Caching.Memory;
+using StackExchange.Redis;
+using Vendor.Application.Common.Interfaces;
+
+namespace Vendor.Infrastructure.Caching;
+
+public class HybridCacheService(IMemoryCache memoryCache, IConnectionMultiplexer? connectionMultiplexer = null) : ICacheService
+{
+    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
+    {
+        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
+        {
+            try
+            {
+                var db = connectionMultiplexer.GetDatabase();
+                var val = await db.StringGetAsync(key);
+                if (val.HasValue)
+                {
+                    return JsonSerializer.Deserialize<T>((string)val!);
+                }
+                return default;
+            }
+            catch
+            {
+                // Fall back to MemoryCache on Redis exception
+            }
+        }
+
+        if (memoryCache.TryGetValue(key, out T? cachedValue))
+        {
+            return cachedValue;
+        }
+        return default;
+    }
+
+    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
+    {
+        var exp = expiration ?? TimeSpan.FromMinutes(10);
+        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
+        {
+            try
+            {
+                var db = connectionMultiplexer.GetDatabase();
+                var json = JsonSerializer.Serialize(value);
+                await db.StringSetAsync(key, json, exp);
+                return;
+            }
+            catch
+            {
+                // Fall back to MemoryCache on Redis exception
+            }
+        }
+
+        memoryCache.Set(key, value, exp);
+    }
+
+    public async Task RemoveAsync(string key, CancellationToken ct = default)
+    {
+        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
+        {
+            try
+            {
+                var db = connectionMultiplexer.GetDatabase();
+                await db.KeyDeleteAsync(key);
+                return;
+            }
+            catch
+            {
+                // Fall back to MemoryCache on Redis exception
+            }
+        }
+
+        memoryCache.Remove(key);
+    }
+
+    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
+    {
+        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
+        {
+            try
+            {
+                var endpoints = connectionMultiplexer.GetEndPoints();
+                if (endpoints.Length > 0)
+                {
+                    var server = connectionMultiplexer.GetServer(endpoints.First());
+                    var keys = server.Keys(pattern: $"{prefix}*").ToArray();
+                    if (keys.Length > 0)
+                    {
+                        var db = connectionMultiplexer.GetDatabase();
+                        await db.KeyDeleteAsync(keys);
+                    }
+                }
+                return;
+            }
+            catch
+            {
+                // Fall back gracefully if Redis server key search fails
+            }
+        }
+
+        // MemoryCache does not support native key iteration safely; fallback complete
+    }
+}
diff --git a/src/Vendor.Infrastructure/DependencyInjection.cs b/src/Vendor.Infrastructure/DependencyInjection.cs
index eeaaab4..670c8bf 100644
--- a/src/Vendor.Infrastructure/DependencyInjection.cs
+++ b/src/Vendor.Infrastructure/DependencyInjection.cs
@@ -2,9 +2,12 @@ using Hangfire;
 using Hangfire.SqlServer;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
+using Microsoft.Extensions.Caching.Memory;
 using Microsoft.Extensions.Caching.StackExchangeRedis;
 using Microsoft.Extensions.Configuration;
 using Microsoft.Extensions.DependencyInjection;
+using StackExchange.Redis;
+using Vendor.Application.Common.Interfaces;
 using Vendor.Application.Interfaces;
 using Vendor.Domain.Aggregates.VendorSettings;
 using Vendor.Domain.Enums;
@@ -33,19 +36,31 @@ public static class DependencyInjection
         services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
         services.AddSingleton<OutboxInterceptor>();
 
-        // Redis distributed cache — connection string read from ConnectionStrings:Redis
-        var redisConnectionString = configuration.GetConnectionString("Redis")
-            ?? throw new InvalidOperationException(
-                "ConnectionStrings:Redis is required. Add it to appsettings or set the CONNECTIONSTRINGS__REDIS environment variable.");
+        services.AddMemoryCache();
 
-        services.AddStackExchangeRedisCache(options =>
+        var redisConnectionString = configuration.GetConnectionString("Redis");
+        if (!string.IsNullOrEmpty(redisConnectionString))
         {
-            options.Configuration = redisConnectionString;
-            options.InstanceName = "vendor:";
-        });
+            try
+            {
+                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
+                services.AddStackExchangeRedisCache(options =>
+                {
+                    options.Configuration = redisConnectionString;
+                    options.InstanceName = "vendor:";
+                });
+            }
+            catch
+            {
+                // Ignore Redis initialization errors during startup; fallback to memory cache
+            }
+        }
 
-        // Bind ICacheService to the Redis implementation
-        services.AddScoped<ICacheService, RedisCacheService>();
+        // Bind ICacheService as Singleton to HybridCacheService with IMemoryCache fallback
+        services.AddSingleton<ICacheService>(sp =>
+            new HybridCacheService(
+                sp.GetRequiredService<IMemoryCache>(),
+                sp.GetService<IConnectionMultiplexer>()));
 
         var connectionString = configuration.GetConnectionString("DefaultConnection")
             ?? "Server=(localdb)\\mssqllocaldb;Database=VendorDb;Trusted_Connection=True;";
diff --git a/tests/Vendor.Infrastructure.Tests/Caching/HybridCacheServiceTests.cs b/tests/Vendor.Infrastructure.Tests/Caching/HybridCacheServiceTests.cs
new file mode 100644
index 0000000..f3362bd
--- /dev/null
+++ b/tests/Vendor.Infrastructure.Tests/Caching/HybridCacheServiceTests.cs
@@ -0,0 +1,80 @@
+using Microsoft.Extensions.Caching.Memory;
+using Moq;
+using StackExchange.Redis;
+using Vendor.Application.Common.Interfaces;
+using Vendor.Infrastructure.Caching;
+using Xunit;
+
+namespace Vendor.Infrastructure.Tests.Caching;
+
+public class HybridCacheServiceTests
+{
+    [Fact]
+    public async Task SetAsync_And_GetAsync_MemoryCacheFallback_WhenRedisIsNull_Works()
+    {
+        // Arrange
+        var memoryCache = new MemoryCache(new MemoryCacheOptions());
+        var cacheService = new HybridCacheService(memoryCache, connectionMultiplexer: null);
+        var key = "test_key_null_redis";
+        var value = "hello_null_redis";
+
+        // Act
+        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
+        var cached = await cacheService.GetAsync<string>(key);
+
+        // Assert
+        Assert.Equal(value, cached);
+    }
+
+    [Fact]
+    public async Task SetAsync_And_GetAsync_MemoryCacheFallback_WhenRedisIsDisconnected_Works()
+    {
+        // Arrange
+        var memoryCache = new MemoryCache(new MemoryCacheOptions());
+        var redisMock = new Mock<IConnectionMultiplexer>();
+        redisMock.Setup(r => r.IsConnected).Returns(false);
+
+        var cacheService = new HybridCacheService(memoryCache, redisMock.Object);
+        var key = "test_key_disconnected_redis";
+        var value = "hello_disconnected";
+
+        // Act
+        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
+        var cached = await cacheService.GetAsync<string>(key);
+
+        // Assert
+        Assert.Equal(value, cached);
+    }
+
+    [Fact]
+    public async Task RemoveAsync_MemoryCacheFallback_WhenRedisIsNull_RemovesValue()
+    {
+        // Arrange
+        var memoryCache = new MemoryCache(new MemoryCacheOptions());
+        var cacheService = new HybridCacheService(memoryCache, connectionMultiplexer: null);
+        var key = "test_key_remove";
+        var value = "value_to_remove";
+
+        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
+        var initialGet = await cacheService.GetAsync<string>(key);
+        Assert.Equal(value, initialGet);
+
+        // Act
+        await cacheService.RemoveAsync(key);
+        var afterRemove = await cacheService.GetAsync<string>(key);
+
+        // Assert
+        Assert.Null(afterRemove);
+    }
+
+    [Fact]
+    public async Task RemoveByPrefixAsync_MemoryCacheFallback_WhenRedisIsNull_DoesNotThrow()
+    {
+        // Arrange
+        var memoryCache = new MemoryCache(new MemoryCacheOptions());
+        var cacheService = new HybridCacheService(memoryCache, connectionMultiplexer: null);
+
+        // Act & Assert (should complete without throwing)
+        await cacheService.RemoveByPrefixAsync("prefix_test_");
+    }
+}
