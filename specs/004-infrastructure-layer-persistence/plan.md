# Implementation Plan: Infrastructure Layer & Persistence

**Branch**: `004-infrastructure-layer-persistence` | **Date**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

---

## Summary

Build the `Vendor.Infrastructure` project to provide production-ready implementations for every interface declared in `Vendor.Domain` (10 repositories, 6 adapters) and `Vendor.Application` (7 core application services). 

Persistence uses EF Core 9 (`Microsoft.EntityFrameworkCore.SqlServer`) against MSSQL with owned types for `Money` and `Address`, JSON columns for primitive collections, global soft-delete query filters, unique indexes, and resilient fault retry. Domain events are persisted atomically via an EF Core `OutboxInterceptor` and dispatched asynchronously by a background `OutboxProcessorHostedService`. Payment gateways (Stripe, PayPal, Paymob) are dynamically resolved via a `PaymentGatewayFactory` with cryptographic webhook signature verification and mandatory idempotency keys. Auth, shipping, dual-mode caching (Memory vs Redis with SignalR backplane), real-time `AdminNotificationHub`, dual-mode email (SendGrid vs MailKit SMTP), and consent-gated analytics flushing are fully implemented.

---

## Technical Context

**Language/Version**: C# 12 / .NET 9 (`net9.0`)

**Primary Dependencies (Infrastructure)**:
- `Vendor.Domain` & `Vendor.Application` (project references)
- `Microsoft.EntityFrameworkCore.SqlServer` 9.x (MSSQL relational provider)
- `Microsoft.EntityFrameworkCore.Design` 9.x (EF Core migrations tooling)
- `Microsoft.Extensions.Caching.StackExchangeRedis` 9.x (`IDistributedCache` Redis provider)
- `Microsoft.AspNetCore.SignalR.StackExchangeRedis` 9.x (SignalR multi-instance backplane)
- `Stripe.net` (Stripe SDK)
- `MailKit` & `MimeKit` (SMTP email sender)
- `SendGrid` (SendGrid email SDK)
- `System.IdentityModel.Tokens.Jwt` (JWT token generation & validation)

**Storage Abstraction**: `VendorDbContext` inheriting `DbContext` and implementing `IUnitOfWork`. Repository implementations (`ProductRepository`, `OrderRepository`, etc.) live in `Vendor.Infrastructure.Persistence.Repositories`.

**Testing**:
- `Vendor.Infrastructure.Tests` → xUnit + Testcontainers (MSSQL & Redis) / EF Core In-Memory database for unit & integration testing. Target coverage ≥ 85%.

**Target Platform**: .NET 9 class library (`Vendor.Infrastructure.csproj`).

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Check | Status |
|-----------|-------|--------|
| I — Clean Architecture | `Vendor.Infrastructure` depends on `Vendor.Domain` and `Vendor.Application`. Neither `Vendor.Domain` nor `Vendor.Application` depends on `Vendor.Infrastructure`. | ✅ PASS |
| II — Result-Oriented Handlers | All infrastructure adapters return `Result<T>` or `Result` when called by application handlers, converting third-party client exceptions into domain `Error` instances. | ✅ PASS |
| III — EF Core Owned Types | `Money` (`Amount`, `Currency`) and `Address` (`Street`, `City`, `State`, `ZipCode`, `CountryCode`) mapped as `OwnsOne` owned types on entity tables. Zero separate VO tables. | ✅ PASS |
| IV — Clone-Per-Vendor | Infrastructure adapters dynamically configure credentials based on `VendorConfig` provided per request or boot context. | ✅ PASS |
| V — Secrets Management | Secret references (`ref:env:`, `ref:vault:`, `ref:aws-ssm:`) resolved via `ISecretResolver` prior to initializing third-party SDK clients. | ✅ PASS |
| VI — Domain Events via Outbox | Outbox pattern implemented via `OutboxInterceptor` inside same SQL transaction; background hosted service dispatches outbox events. | ✅ PASS |
| VII — Test Coverage ≥ 85% Infrastructure | Unit and integration test suite covering repositories, outbox interceptor, payment factories, token service, and cache fallback. | ✅ PASS |

**Gate result: ALL PASS — proceed to Phase 0.**

---

## Project Structure

### Documentation (this feature)

```text
specs/004-infrastructure-layer-persistence/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── infrastructure-services.md
│   └── webhook-signatures.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
├── Vendor.Infrastructure/               # ← THIS FEATURE
│   ├── Persistence/
│   │   ├── Configurations/              # EF Core EntityTypeConfigurations
│   │   ├── Repositories/                  # 10 Repository implementations
│   │   ├── ValueConverters/
│   │   └── VendorDbContext.cs
│   ├── Outbox/
│   │   ├── OutboxMessage.cs
│   │   ├── OutboxInterceptor.cs
│   │   └── OutboxProcessorHostedService.cs
│   ├── Payments/
│   │   ├── StripePaymentGateway.cs
│   │   ├── PayPalPaymentGateway.cs
│   │   ├── PaymobPaymentGateway.cs
│   │   └── PaymentGatewayFactory.cs
│   ├── Shipping/
│   │   ├── FlatRateShippingProvider.cs
│   │   └── ShippoShippingProvider.cs
│   ├── Auth/
│   │   ├── JwtTokenService.cs
│   │   └── ExternalAuthService.cs
│   ├── Caching/
│   │   ├── InMemoryCacheService.cs
│   │   ├── RedisCacheService.cs
│   │   └── CacheInvalidationHandler.cs
│   ├── Realtime/
│   │   ├── AdminNotificationHub.cs
│   │   └── SignalRRealtimeNotifier.cs
│   ├── Email/
│   │   ├── SendGridEmailSender.cs
│   │   └── SmtpEmailSender.cs
│   ├── Analytics/
│   │   └── AnalyticsProcessorHostedService.cs
│   └── DependencyInjection.cs
│
├── Vendor.Domain/                       # (Completed in Feature 002)
├── Vendor.Application/                  # (Completed in Feature 003)
└── Vendor.Api/                          # Next feature

tests/
├── Vendor.Infrastructure.Tests/          # Unit & Integration tests, ≥85% coverage
│   ├── Persistence/
│   │   ├── DbContextTests.cs
│   │   └── OutboxTests.cs
│   ├── Payments/
│   │   └── WebhookValidationTests.cs
│   └── Auth/
│       └── JwtTokenServiceTests.cs
```

---

## Complexity Tracking

No constitution violations. EF Core owned types, Outbox pattern, and dynamic payment gateway factory adhere strictly to Principles I, III, V, and VI.

---

## Post-Phase 1 Constitution Re-check

*Performed after design artifacts (research.md, data-model.md, contracts/) are complete.*

| Principle | Design Verification | Status |
|-----------|---------------------|--------|
| I — Clean Architecture | `Vendor.Infrastructure` depends ONLY on `Vendor.Domain` & `Vendor.Application`. No dependencies leak upwards. | ✅ PASS |
| II — Result-Oriented Handlers | All infrastructure adapters map third-party client exceptions to `Result.Failure(Error)` instances. | ✅ PASS |
| III — EF Core Owned Types | `Money` and `Address` mapped via `OwnsOne` directly onto aggregate table columns. Zero separate VO tables. | ✅ PASS |
| IV — Clone-Per-Vendor | Dynamically resolves payment/shipping/email credentials from `VendorConfig`. Zero hardcoded vendor credentials. | ✅ PASS |
| V — Secrets | Secret references (`ref:env:`, `ref:vault:`, `ref:aws-ssm:`) resolved via `ISecretResolver` prior to API calls. | ✅ PASS |
| VI — Outbox | `OutboxInterceptor` intercepts EF `SavingChangesAsync` and writes `OutboxMessage` rows in exact same DB transaction. | ✅ PASS |
| VII — ≥85% Coverage Target | Integration test suite leveraging EF Core In-Memory / Testcontainers documented in quickstart.md. | ✅ PASS |

**Post-design gate result: ALL PASS — proceed to `/speckit-tasks`.**
