# Design Document — Phase 1: Core Reliability & Outbox Engine

**Feature**: Phase 1 Core Reliability
**Date**: 2026-08-03
**Status**: Approved

## 1. Executive Summary

Phase 1 establishes enterprise core infrastructure for the vendor e-commerce platform by introducing an automated background outbox processing engine powered by Hangfire, a resilient hybrid caching abstraction supporting Redis with `IMemoryCache` fallback, and HTTP rate limiting policies across public and administrative API routes.

---

## 2. Architecture & Subsystems

### 2.1 Outbox Processing Engine (Hangfire)

#### Components
- `OutboxProcessorJob`: Background worker job registered with Hangfire.
- `OutboxCleanupJob`: Daily recurring maintenance job purging stale outbox records.
- `HangfireDashboardAuthorizationFilter`: Security filter restricting `/hangfire` access to authenticated `VendorAdmin` users.

#### Workflow & Execution Rules
1. **Batch Fetching**: Every 5 seconds, Hangfire triggers `OutboxProcessorJob.ExecuteAsync()`. The job fetches up to **50 `Pending` outbox messages** ordered by `CreatedAtUtc`.
2. **Domain Event Dispatching**: Deserializes `Type` and `Content` JSON into an `IDomainEvent` instance and dispatches it via MediatR `IPublisher.Publish(domainEvent, ct)`.
3. **Status & Retry Lifecycle**:
   - **Success**: Updates message `Status = OutboxMessageStatus.Processed` and `ProcessedAtUtc = DateTime.UtcNow`.
   - **Failure**: Increments `RetryCount` and records exception message in `Error`. Calculates exponential backoff delay (`1s`, `5s`, `25s`, `2m`, `10m`).
   - **Dead-Letter**: If `RetryCount >= 5`, updates `Status = OutboxMessageStatus.DeadLetter` for manual inspection and retry via the `/hangfire` dashboard.
4. **Maintenance Cleanup**: `OutboxCleanupJob` runs daily at 02:00 AM UTC, executing `DELETE FROM OutboxMessages WHERE Status = 'Processed' AND ProcessedAtUtc < DATEADD(day, -7, GETUTCDATE())`.

---

### 2.2 Hybrid Caching Infrastructure (`ICacheService`)

#### Contract (`Vendor.Application.Common.Interfaces.ICacheService`)
```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
```

#### Provider Strategy (`HybridCacheService`)
- Evaluates `IConnectionMultiplexer` (Redis) connectivity on demand.
- **Redis Path**: When Redis connection is healthy, uses Redis string commands with JSON serialization for distributed cross-instance caching.
- **In-Memory Fallback**: When Redis connection string is omitted (`ref:env:REDIS_CONNECTION_STRING` missing or unresolved) or connection is offline, seamlessly routes operations to `IMemoryCache`.

---

### 2.3 Rate Limiting Policies (`Microsoft.AspNetCore.RateLimiting`)

#### Endpoints & Rate Limits
1. **Authentication Endpoints (`/api/v1/auth/login`, `/api/v1/auth/refresh`)**:
   - **Policy**: `FixedWindow` (5 requests per 1 minute window per IP address).
2. **Cart & Checkout Endpoints (`/api/v1/cart/*`, `/api/v1/orders/checkout`)**:
   - **Policy**: `TokenBucket` (30 requests per 1 minute window, refill rate 5/sec, burst capacity 10 per IP address).
3. **Admin Administrative Endpoints (`/api/v1/admin/*`)**:
   - **Policy**: `SlidingWindow` (100 requests per 1 minute window per Admin User ID).

#### Response & Error Handling
Exceeding rate limits immediately short-circuits the pipeline and returns `HTTP 429 Too Many Requests` with problem details payload and `Retry-After` header.

---

## 3. Testing Strategy

1. **Unit Tests (`Vendor.Application.Tests`)**:
   - Outbox deserialization and MediatR event dispatching.
   - Max retry threshold enforcement (switching status to `DeadLetter` at 5 attempts).
   - `HybridCacheService` behavior under active Redis vs fallback MemoryCache.
2. **Integration Tests (`Vendor.Api.Tests`)**:
   - Rate limiting middleware HTTP 429 rejection on `/api/v1/auth/login` after 5 rapid attempts.
   - Hangfire dashboard route security authorization filter verification.
   - Full end-to-end outbox pipeline execution test.
