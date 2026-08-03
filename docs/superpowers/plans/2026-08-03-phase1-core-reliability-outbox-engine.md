# Phase 1: Core Reliability & Outbox Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-grade automated outbox processing worker engine with Hangfire, a hybrid Redis/MemoryCache distributed caching service, and endpoint rate limiting middleware.

**Architecture:** Use Hangfire SQL Server storage for outbox background worker scheduling and dead-letter queue management. Implement a hybrid caching abstraction (`ICacheService`) that auto-detects Redis connection health with fallback to `IMemoryCache`. Configure ASP.NET Core `RateLimiter` policies across public auth, cart/checkout, and admin Minimal API routes.

**Tech Stack:** .NET 9, Hangfire.Core, Hangfire.SqlServer, Hangfire.AspNetCore, StackExchange.Redis, MediatR, FluentAssertions, xUnit.

## Global Constraints

- Solution file: `Vendor.slnx`
- Target framework: `net9.0`
- Zero build warnings or broken tests (`dotnet test Vendor.slnx` must pass 100%)
- Exact file paths and types must match Clean Architecture boundaries

---

### Task 1: Hangfire Setup & Outbox Processor Job

**Files:**
- Modify: `src/Vendor.Infrastructure/Vendor.Infrastructure.csproj` (add Hangfire NuGet packages)
- Modify: `src/Vendor.Api/Vendor.Api.csproj` (add Hangfire.AspNetCore)
- Create: `src/Vendor.Infrastructure/Outbox/OutboxProcessorJob.cs`
- Create: `src/Vendor.Infrastructure/Outbox/OutboxCleanupJob.cs`
- Create: `src/Vendor.Api/Security/HangfireDashboardAuthorizationFilter.cs`
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Modify: `src/Vendor.Api/Program.cs`
- Create: `tests/Vendor.Infrastructure.Tests/Outbox/OutboxProcessorJobTests.cs`

**Interfaces:**
- Consumes: `VendorDbContext`, `IPublisher` (MediatR), `OutboxMessage`
- Produces: `OutboxProcessorJob.ProcessOutboxMessagesAsync(CancellationToken ct)`

- [ ] **Step 1: Write failing unit test for OutboxProcessorJob**

Create `tests/Vendor.Infrastructure.Tests/Outbox/OutboxProcessorJobTests.cs`:
```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Vendor.Domain.Abstractions;
using Vendor.Infrastructure.Outbox;
using Vendor.Infrastructure.Persistence;
using Xunit;

namespace Vendor.Infrastructure.Tests.Outbox;

public class OutboxProcessorJobTests
{
    private static VendorDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new VendorDbContext(options);
    }

    public record TestDomainEvent(Guid Id) : IDomainEvent;

    [Fact]
    public async Task ProcessOutboxMessagesAsync_DispatchesEvents_And_MarksProcessed()
    {
        using var context = CreateInMemoryDbContext();
        var publisherMock = new Mock<IPublisher>();

        var evt = new TestDomainEvent(Guid.NewGuid());
        var json = System.Text.Json.JsonSerializer.Serialize(evt, evt.GetType());

        var message = new OutboxMessage(
            Guid.NewGuid(),
            evt.GetType().AssemblyQualifiedName!,
            json,
            DateTime.UtcNow);

        await context.OutboxMessages.AddAsync(message);
        await context.SaveChangesAsync();

        var job = new OutboxProcessorJob(context, publisherMock.Object);
        await job.ProcessOutboxMessagesAsync(CancellationToken.None);

        var updated = await context.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(updated);
        Assert.Equal(OutboxMessageStatus.Processed, updated.Status);
        Assert.NotNull(updated.ProcessedAtUtc);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~OutboxProcessorJobTests"`
Expected: FAIL (compilation error: `OutboxProcessorJob` does not exist).

- [ ] **Step 3: Add Hangfire package references and implement OutboxProcessorJob**

In `src/Vendor.Infrastructure/Vendor.Infrastructure.csproj`:
Add `<PackageReference Include="Hangfire.Core" Version="1.8.18" />` and `<PackageReference Include="Hangfire.SqlServer" Version="1.8.18" />`.

In `src/Vendor.Api/Vendor.Api.csproj`:
Add `<PackageReference Include="Hangfire.AspNetCore" Version="1.8.18" />`.

Create `src/Vendor.Infrastructure/Outbox/OutboxProcessorJob.cs`:
```csharp
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Abstractions;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Outbox;

public class OutboxProcessorJob(VendorDbContext dbContext, IPublisher publisher)
{
    public async Task ProcessOutboxMessagesAsync(CancellationToken ct = default)
    {
        var messages = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type);
                if (type == null)
                {
                    message.MarkAsFailed($"Type '{message.Type}' could not be loaded.");
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(message.Content, type) as IDomainEvent;
                if (domainEvent == null)
                {
                    message.MarkAsFailed($"Failed to deserialize outbox message payload.");
                    continue;
                }

                await publisher.Publish(domainEvent, ct);
                message.MarkAsProcessed();
            }
            catch (Exception ex)
            {
                message.MarkAsFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
```

Create `src/Vendor.Infrastructure/Outbox/OutboxCleanupJob.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Outbox;

public class OutboxCleanupJob(VendorDbContext dbContext)
{
    public async Task PurgeOldProcessedMessagesAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var oldMessages = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAtUtc < cutoff)
            .ToListAsync(ct);

        if (oldMessages.Count > 0)
        {
            dbContext.OutboxMessages.RemoveRange(oldMessages);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
```

Create `src/Vendor.Api/Security/HangfireDashboardAuthorizationFilter.cs`:
```csharp
using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Vendor.Api.Security;

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize([NotNull] DashboardAuthorizeContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.RequestHost.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            httpContext.RequestHost.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return httpContext.User.Identity?.IsAuthenticated == true &&
               httpContext.User.IsInRole("VendorAdmin");
    }
}
```

Register Hangfire services in `Vendor.Infrastructure/DependencyInjection.cs` and `Vendor.Api/Program.cs`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~OutboxProcessorJobTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(outbox): implement Hangfire outbox processor worker and cleanup jobs"
```

---

### Task 2: Hybrid Cache Service (`ICacheService`)

**Files:**
- Create: `src/Vendor.Application/Common/Interfaces/ICacheService.cs`
- Create: `src/Vendor.Infrastructure/Caching/HybridCacheService.cs`
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Create: `tests/Vendor.Infrastructure.Tests/Caching/HybridCacheServiceTests.cs`

**Interfaces:**
- Consumes: `IMemoryCache`, `IConnectionMultiplexer` (optional)
- Produces: `ICacheService` (`GetAsync`, `SetAsync`, `RemoveAsync`, `RemoveByPrefixAsync`)

- [ ] **Step 1: Write failing unit test for HybridCacheService**

Create `tests/Vendor.Infrastructure.Tests/Caching/HybridCacheServiceTests.cs`:
```csharp
using Microsoft.Extensions.Caching.Memory;
using Vendor.Infrastructure.Caching;
using Xunit;

namespace Vendor.Infrastructure.Tests.Caching;

public class HybridCacheServiceTests
{
    [Fact]
    public async Task SetAsync_And_GetAsync_MemoryCacheFallback_Works()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheService = new HybridCacheService(memoryCache, connectionMultiplexer: null);

        var key = "test_key_1";
        var value = "hello_cache";

        await cacheService.SetAsync(key, value, TimeSpan.FromMinutes(5));
        var cached = await cacheService.GetAsync<string>(key);

        Assert.Equal(value, cached);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~HybridCacheServiceTests"`
Expected: FAIL (`ICacheService` / `HybridCacheService` missing).

- [ ] **Step 3: Create ICacheService and HybridCacheService**

Create `src/Vendor.Application/Common/Interfaces/ICacheService.cs`:
```csharp
namespace Vendor.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
```

Create `src/Vendor.Infrastructure/Caching/HybridCacheService.cs`:
```csharp
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
            var db = connectionMultiplexer.GetDatabase();
            var val = await db.StringGetAsync(key);
            if (val.HasValue)
            {
                return JsonSerializer.Deserialize<T>((string)val!);
            }
            return default;
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
            var db = connectionMultiplexer.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, json, exp);
            return;
        }

        memoryCache.Set(key, value, exp);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
        {
            var db = connectionMultiplexer.GetDatabase();
            await db.KeyDeleteAsync(key);
            return;
        }

        memoryCache.Remove(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (connectionMultiplexer != null && connectionMultiplexer.IsConnected)
        {
            var server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{prefix}*").ToArray();
            if (keys.Length > 0)
            {
                var db = connectionMultiplexer.GetDatabase();
                await db.KeyDeleteAsync(keys);
            }
            return;
        }

        // MemoryCache does not support native key iteration safely; no-op or clear via pattern tracking
    }
}
```

Register `ICacheService` in `Vendor.Infrastructure/DependencyInjection.cs`:
```csharp
services.AddSingleton<ICacheService, HybridCacheService>();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vendor.Infrastructure.Tests --filter "FullyQualifiedName~HybridCacheServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(caching): add HybridCacheService supporting Redis with IMemoryCache fallback"
```

---

### Task 3: Rate Limiting Middleware Integration

**Files:**
- Create: `src/Vendor.Api/Extensions/RateLimitingExtensions.cs`
- Modify: `src/Vendor.Api/Program.cs`
- Modify: `src/Vendor.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/Vendor.Api/Endpoints/CartEndpoints.cs`
- Modify: `src/Vendor.Api/Endpoints/ProductEndpoints.cs`
- Create: `tests/Vendor.Api.Tests/Integration/RateLimitingTests.cs`

**Interfaces:**
- Consumes: `Microsoft.AspNetCore.RateLimiting`
- Produces: Rate limiting policy configurations (`auth-policy`, `cart-checkout-policy`, `admin-policy`)

- [ ] **Step 1: Write failing integration test for Auth Rate Limiting**

Create `tests/Vendor.Api.Tests/Integration/RateLimitingTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class RateLimitingTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public RateLimitingTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthEndpoint_ExceedingLimit_Returns429TooManyRequests()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 7; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { Email = "test@example.com", Password = "Pass" });
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Vendor.Api.Tests --filter "FullyQualifiedName~RateLimitingTests"`
Expected: FAIL (returns 400 Bad Request instead of 429 Too Many Requests).

- [ ] **Step 3: Create RateLimitingExtensions and apply policies**

Create `src/Vendor.Api/Extensions/RateLimitingExtensions.cs`:
```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Vendor.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth-policy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("cart-checkout-policy", httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 30,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 30,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}
```

Apply `.RequireRateLimiting("auth-policy")` in `AuthEndpoints.cs` and `.RequireRateLimiting("cart-checkout-policy")` in `CartEndpoints.cs`.

In `src/Vendor.Api/Program.cs`:
```csharp
builder.Services.AddCustomRateLimiting();
...
app.UseRateLimiter();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Vendor.Api.Tests --filter "FullyQualifiedName~RateLimitingTests"`
Expected: PASS.

- [ ] **Step 5: Run full test suite and Commit**

Run: `dotnet test Vendor.slnx`
Expected: ALL tests pass.

```bash
git add .
git commit -m "feat(rate-limiting): add endpoint rate limiting policies with 429 response handling"
```
