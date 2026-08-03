# Task 2 Report: Hybrid Cache Service (`ICacheService`)

**Status:** DONE  
**Date:** 2026-08-03  
**Commit:** `fix(caching): address code review feedback for lazy DI resolution and memory cache eviction`

---

## Executive Summary

Task 2 of Phase 1 Core Reliability & Outbox Engine has been fully implemented, reviewed, and enhanced based on code review feedback. The `ICacheService` contract is defined in `Vendor.Application.Common.Interfaces`. `HybridCacheService` provides robust Redis caching with seamless fallback to `IMemoryCache`. Lazy DI factory resolution with `AbortOnConnectFail = false` guarantees startup and runtime resilience when Redis is unreachable, and stale local memory cache entries are automatically evicted on write/delete operations.

---

## Key Artifacts & Changes

### 1. Application Layer Interface
- **`src/Vendor.Application/Common/Interfaces/ICacheService.cs`**:
  - Declared `ICacheService` interface contract:
    - `Task<T?> GetAsync<T>(string key, CancellationToken ct = default);`
    - `Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);`
    - `Task RemoveAsync(string key, CancellationToken ct = default);`
    - `Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);`
- **`src/Vendor.Application/Interfaces/IApplicationInterfaces.cs`**:
  - Removed duplicate `ICacheService` declaration to maintain a single source of truth in `Vendor.Application.Common.Interfaces`.

### 2. Infrastructure Layer & Hybrid Caching
- **`src/Vendor.Infrastructure/Caching/HybridCacheService.cs`**:
  - Primary Redis strategy (`IConnectionMultiplexer`) with `IMemoryCache` fallback.
  - Evicts stale local entries from `IMemoryCache` via `memoryCache.Remove(key)` during `SetAsync` and `RemoveAsync` operations when Redis writes succeed.
  - Exception-resilient: Catches runtime Redis exceptions (`RedisConnectionException`, timeouts) during `GetAsync`, `SetAsync`, `RemoveAsync`, and `RemoveByPrefixAsync`, falling back safely to `IMemoryCache`.
- **`src/Vendor.Infrastructure/DependencyInjection.cs`**:
  - Configured `IConnectionMultiplexer` as a lazy factory delegate setting `AbortOnConnectFail = false` and returning `null` on connection errors instead of throwing 500 error on startup/runtime resolution.
  - Registered `ICacheService` as `Singleton` mapped to `HybridCacheService`.

---

## Verification & Test Results

### Unit Tests
- File: `tests/Vendor.Infrastructure.Tests/Caching/HybridCacheServiceTests.cs`
- Test Scenarios:
  - `SetAsync_And_GetAsync_MemoryCacheFallback_WhenRedisIsNull_Works`: Verifies `IMemoryCache` fallback when `IConnectionMultiplexer` is null.
  - `SetAsync_And_GetAsync_MemoryCacheFallback_WhenRedisIsDisconnected_Works`: Verifies `IMemoryCache` fallback when `IConnectionMultiplexer.IsConnected` is false.
  - `SetAsync_And_GetAsync_HandlesRuntimeRedisConnectionFailure_Gracefully`: Verifies fallback when Redis operations throw `RedisConnectionException` at runtime.
  - `SetAsync_EvictsStaleMemoryCache_WhenRedisSucceeds`: Verifies eviction of stale `IMemoryCache` entries when writing to Redis.
  - `RemoveAsync_MemoryCacheFallback_WhenRedisIsNull_RemovesValue`: Verifies key eviction on `IMemoryCache` fallback.
  - `RemoveByPrefixAsync_MemoryCacheFallback_WhenRedisIsNull_DoesNotThrow`: Verifies non-blocking execution when clearing by prefix on fallback.

### Suite Run (`dotnet test Vendor.slnx`)
- **Vendor.Domain.Tests**: 75/75 passed
- **Vendor.Application.Tests**: 52/52 passed
- **Vendor.Infrastructure.Tests**: 29/29 passed (including 6 caching unit tests)
- **Vendor.Api.Tests**: 31/31 passed
- **Total:** 187/187 tests passed (100% success rate, 0 failures).

---

## Next Steps

Proceed to Task 3 of Phase 1: Rate Limiting Middleware Integration (`Microsoft.AspNetCore.RateLimiting` policies for auth, cart/checkout, and admin routes).
