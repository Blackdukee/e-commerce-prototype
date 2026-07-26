# Quickstart Validation Guide: Infrastructure Layer & Persistence

**Feature**: 004-infrastructure-layer-persistence  
**Branch**: `004-infrastructure-layer-persistence`  

This guide describes how to validate that the Infrastructure layer implementation is correct end-to-end using integration test suites, in-memory databases, and Testcontainers.

---

## Prerequisites

| Requirement | Detail |
|-------------|--------|
| .NET SDK | 9.0 (latest) |
| SQL Server | MSSQL Server 2022 / LocalDB or Docker (`mcr.microsoft.com/mssql/server:2022-latest`) |
| Redis (Optional for integration tests) | Redis 7.x or Docker (`redis:7-alpine`) |
| `Vendor.Infrastructure` project | Dependencies: `Vendor.Domain`, `Vendor.Application`, `Microsoft.EntityFrameworkCore.SqlServer`, `MailKit`, `StackExchange.Redis` |
| `Vendor.Infrastructure.Tests` project | xUnit, FluentAssertions, Testcontainers / EF Core In-Memory |

---

## Setup

```powershell
# From repo root — build Infrastructure project
dotnet build src/Vendor.Infrastructure/Vendor.Infrastructure.csproj

# Build Infrastructure test project
dotnet build tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj
```

---

## Validation Scenarios

Run all infrastructure layer tests:
```powershell
dotnet test tests/Vendor.Infrastructure.Tests/ --logger "console;verbosity=normal"
```

### Scenario 1 — EF Core Entity Mappings & Value Object Persistence

**Test class**: `DbContextTests`

| Step | Assertion |
|------|-----------|
| Create `Product` with `Money` base price, `Slug`, `Images` list | Entity persisted to MSSQL |
| Query database directly via raw SQL / DbContext | `PriceAmount` and `PriceCurrency` stored on `Products` table; `Images` stored as `nvarchar(max)` JSON |
| Perform soft delete `Product.IsDeleted = true` | `ProductRepository.GetByIdAsync` returns `null` due to global EF Core query filter |

---

### Scenario 2 — Transactional Outbox Interceptor & Background Dispatcher

**Test class**: `OutboxTests`

| Step | Assertion |
|------|-----------|
| Mutate aggregate and save via `VendorDbContext.SaveChangesAsync` | `OutboxInterceptor` extracts raised domain events |
| Query `OutboxMessages` table | `OutboxMessage` row inserted in same DB transaction with `ProcessedOnUtc == null` |
| Trigger `OutboxProcessorHostedService.ProcessNextBatchAsync` | Event published via MediatR; `ProcessedOnUtc` updated with timestamp |

---

### Scenario 3 — Payment Gateway Factory & Webhook Signature Validation

**Test class**: `WebhookValidationTests`

| Step | Assertion |
|------|-----------|
| Pass `Stripe` payload with valid `Stripe-Signature` | `StripePaymentGateway.ValidateWebhook` returns `true` |
| Pass `Stripe` payload with invalid signature | `StripePaymentGateway.ValidateWebhook` returns `false` |
| Pass `Paymob` payload with valid HMAC SHA-512 | `PaymobPaymentGateway.ValidateWebhook` returns `true` |
| Resolve payment gateway via `PaymentGatewayFactory` for "stripe" | Returns `StripePaymentGateway` instance |

---

### Scenario 4 — Dual-Mode Caching & SignalR Backplane Configuration

**Test class**: `CacheServiceTests`

| Step | Assertion |
|------|-----------|
| Set `Caching:Provider = "Memory"` | `ICacheService` resolved as `InMemoryCacheService` |
| Set `Caching:Provider = "Redis"` | `ICacheService` resolved as `RedisCacheService` |
| Mutate `Product` aggregate | `ProductUpdatedEventHandler` invalidates `products:listings` cache key |

---

### Scenario 5 — JWT Token Generation & Refresh Token Rotation

**Test class**: `JwtTokenServiceTests`

| Step | Assertion |
|------|-----------|
| Call `JwtTokenService.GenerateTokens` | Returns 30-min access token containing `sub`, `email`, `role` claims and 64-byte refresh token |
| Call `RefreshTokenAsync` with valid refresh token | Generates new token pair, revokes old refresh token in DB (`IsRevoked = true`) |

---

## Coverage Gate

Verify Infrastructure layer coverage meets ≥ 85%:

```powershell
dotnet test tests/Vendor.Infrastructure.Tests/ \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

reportgenerator -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:TextSummary

Get-Content coverage/report/Summary.txt
```

**Expected**: `Line coverage: ≥ 85.0%` for `Vendor.Infrastructure` assembly.

---

## Next Steps

After all validation scenarios pass and coverage ≥ 85%:

```bash
/speckit-tasks
```
