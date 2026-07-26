# Implementation Plan: Core Domain Layer Aggregates

**Branch**: `002-domain-layer-aggregates` | **Date**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-domain-layer-aggregates/spec.md`

---

## Summary

Implement 11 DDD aggregate roots (`Product`, `ProductVariant`, `Customer`, `Cart`, `Order`, `Payment`, `Shipment`, `Promotion`, `ReturnRequest`, `VendorSettings`, `AnalyticsEvent`) with strongly-typed IDs, 6 value objects (`Money`, `Address`, `DateRange`, `Slug`, `Weight`, `Dimensions`), 22 domain events, 10 repository interfaces, and 6 adapter interfaces — all in `Vendor.Domain` with **zero external NuGet dependencies** (BCL / `net9.0` only).

The technical approach:
- Strongly-typed IDs as `readonly record struct` wrapping `Guid` (zero-NuGet, struct equality, no heap allocation).
- `AggregateRoot<TId>` base class collecting domain events via `List<IDomainEvent>` (cleared by Infrastructure after outbox enqueue).
- Value objects implemented as `readonly record struct` (Money, DateRange, Slug, Weight, Dimensions) or `sealed record` (Address — mutable set via EF Core).
- Order state machine enforced via a static `AllowedTransitions` dictionary in the `Order` aggregate.
- Cart abandonment exposed as an `IsAbandoned(DateTimeOffset utcNow)` pure predicate (no infra dependency).

---

## Technical Context

**Language/Version**: C# 12 / .NET 9 (`net9.0`)

**Primary Dependencies (Domain)**: None — BCL only (`System`, `System.Collections.Generic`, `System.Text.RegularExpressions`, `System.Text.Json` for event payloads). No NuGet packages.

**Primary Dependencies (Application)**: MediatR 12, FluentValidation 11 (abstractions only — no EF Core, no HTTP clients).

**Primary Dependencies (Infrastructure)**: EF Core 9 (MSSQL provider), MediatR 12, Polly 8 (retry), outbox dispatcher background service.

**Storage**: Microsoft SQL Server — code-first EF Core migrations. Value objects mapped as owned types (no separate tables). Aggregate roots each get a dedicated `IEntityTypeConfiguration<T>`.

**Testing**:
- `Vendor.Domain.Tests` → xUnit, zero mocks required (pure unit tests).
- `Vendor.Application.Tests` → xUnit + NSubstitute (repository/adapter fakes).
- `Vendor.Infrastructure.Tests` → xUnit + EF Core InMemory / LocalDB for integration.

**Target Platform**: Linux/Windows container (Docker), .NET 9 runtime.

**Project Type**: Class library (`Vendor.Domain`) — no executable entry point; consumed by `Vendor.Application` → `Vendor.Infrastructure` → `Vendor.Api`.

**Performance Goals**: Domain layer is compute-only; individual aggregate operation latency < 1 ms (pure in-memory invariant checks). No I/O in Domain layer.

**Constraints**: Zero external NuGet packages in `Vendor.Domain.csproj`. EF Core owned types must not produce separate tables for value objects.

**Scale/Scope**: Single-tenant per deployment; domain model supports up to ~50k orders/day per vendor instance. 11 aggregate roots, 22 domain events, 16 interfaces.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Check | Status |
|-----------|-------|--------|
| I — Clean Architecture | `Vendor.Domain` has no references to Application/Infrastructure/API projects or any external NuGet packages. | ✅ PASS |
| II — Result-Oriented Handlers | Domain aggregates throw domain exceptions for invariant violations (correct for Domain layer); Application handlers return `Result<T>` — no handlers in this feature. | ✅ PASS |
| III — EF Core Owned Types | `Money`, `Address`, `DateRange`, `Slug`, `Weight`, `Dimensions` will be mapped as owned types on aggregate tables. Mapping config lives in Infrastructure. | ✅ PASS |
| IV — Clone-Per-Vendor | Zero vendor-identity conditionals in domain model; all configurable thresholds (low-stock threshold, max cart items) injected as parameters or via VendorSettings. | ✅ PASS |
| V — Secrets Management | Domain layer has no secret handling. Secrets belong to Infrastructure. | ✅ N/A |
| VI — Domain Events via Outbox | All aggregates use `AddDomainEvent(IDomainEvent)` pattern; Infrastructure `SaveChangesAsync` intercept enqueues events to outbox before committing. Domain never dispatches directly. | ✅ PASS |
| VII — Test Coverage ≥ 90% Domain | Pure unit tests — all invariant branches testable without infrastructure. | ✅ PASS |

**Gate result: ALL PASS — proceed to Phase 0.**

---

## Project Structure

### Documentation (this feature)

```text
specs/002-domain-layer-aggregates/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── domain-event-catalog.md
│   └── repository-adapter-interfaces.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
├── Vendor.Domain/                        # ← THIS FEATURE (zero NuGet deps)
│   ├── Abstractions/
│   │   ├── AggregateRoot.cs             # AggregateRoot<TId> base class
│   │   ├── Entity.cs                    # Entity<TId> base class
│   │   └── IDomainEvent.cs              # Marker interface
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   ├── Address.cs
│   │   ├── DateRange.cs
│   │   ├── Slug.cs
│   │   ├── Weight.cs
│   │   └── Dimensions.cs
│   ├── Aggregates/
│   │   ├── Product/
│   │   │   ├── Product.cs
│   │   │   ├── ProductId.cs
│   │   │   ├── ProductVariant.cs
│   │   │   └── ProductVariantId.cs
│   │   ├── Customer/
│   │   │   ├── Customer.cs
│   │   │   └── CustomerId.cs
│   │   ├── Cart/
│   │   │   ├── Cart.cs
│   │   │   ├── CartId.cs
│   │   │   └── CartItem.cs
│   │   ├── Order/
│   │   │   ├── Order.cs
│   │   │   ├── OrderId.cs
│   │   │   ├── OrderLine.cs
│   │   │   └── OrderStatus.cs
│   │   ├── Payment/
│   │   │   ├── Payment.cs
│   │   │   ├── PaymentId.cs
│   │   │   └── PaymentStatus.cs
│   │   ├── Shipment/
│   │   │   ├── Shipment.cs
│   │   │   ├── ShipmentId.cs
│   │   │   └── ShipmentStatus.cs
│   │   ├── Promotion/
│   │   │   ├── Promotion.cs
│   │   │   └── PromotionId.cs
│   │   ├── ReturnRequest/
│   │   │   ├── ReturnRequest.cs
│   │   │   ├── ReturnRequestId.cs
│   │   │   └── ReturnItem.cs
│   │   ├── VendorSettings/
│   │   │   ├── VendorSettings.cs           # (extends Feature 001 aggregate)
│   │   │   └── VendorSettingsId.cs
│   │   └── AnalyticsEvent/
│   │       ├── AnalyticsEvent.cs
│   │       └── AnalyticsEventId.cs
│   ├── Events/
│   │   ├── Product/    (ProductActivated, ProductDeactivated, ProductLowStock)
│   │   ├── Customer/   (CustomerCreated, CustomerConsentUpdated)
│   │   ├── Order/      (OrderPlaced, OrderConfirmed, OrderShipped, OrderDelivered, OrderCancelled, OrderRefundRequested)
│   │   ├── Payment/    (PaymentCaptured, PaymentFailed, PaymentRefunded)
│   │   ├── Shipment/   (ShipmentInTransit, ShipmentDelivered)
│   │   ├── Promotion/  (PromotionExhausted)
│   │   ├── ReturnRequest/ (ReturnRequestCreated, ReturnRequestApproved, ReturnCompleted, ExchangeCompleted)
│   │   └── VendorSettings/ (VendorSettingsUpdated)
│   ├── Interfaces/
│   │   ├── Repositories/
│   │   │   ├── IProductRepository.cs
│   │   │   ├── ICustomerRepository.cs
│   │   │   ├── ICartRepository.cs
│   │   │   ├── IOrderRepository.cs
│   │   │   ├── IPaymentRepository.cs
│   │   │   ├── IShipmentRepository.cs
│   │   │   ├── IPromotionRepository.cs
│   │   │   ├── IReturnRequestRepository.cs
│   │   │   ├── IVendorSettingsRepository.cs
│   │   │   └── IAnalyticsEventRepository.cs
│   │   └── Adapters/
│   │       ├── IPaymentGateway.cs
│   │       ├── IShippingProvider.cs
│   │       ├── ITaxCalculator.cs
│   │       ├── IAnalyticsForwarder.cs
│   │       ├── INotificationSender.cs
│   │       └── ISecretResolver.cs           # (already exists from Feature 001)
│   └── Exceptions/
│       ├── DomainException.cs
│       ├── CurrencyMismatchException.cs
│       └── InvalidStateTransitionException.cs
│
├── Vendor.Application/                   # Command/Query handlers (next feature)
├── Vendor.Infrastructure/                # EF Core configs (next feature)
└── Vendor.Api/                           # Endpoints (next feature)

tests/
├── Vendor.Domain.Tests/                  # ← Pure unit tests, 90%+ coverage
│   ├── Aggregates/
│   │   ├── ProductTests.cs
│   │   ├── CustomerTests.cs
│   │   ├── CartTests.cs
│   │   ├── OrderTests.cs
│   │   ├── PaymentTests.cs
│   │   ├── ShipmentTests.cs
│   │   ├── PromotionTests.cs
│   │   ├── ReturnRequestTests.cs
│   │   └── AnalyticsEventTests.cs
│   └── ValueObjects/
│       ├── MoneyTests.cs
│       ├── SlugTests.cs
│       └── DateRangeTests.cs
└── Vendor.Application.Tests/             # Next feature
```

**Structure Decision**: Extends the existing Clean Architecture solution (`Vendor.Domain`, `Vendor.Application`, `Vendor.Infrastructure`, `Vendor.Api`). All domain code lives inside `src/Vendor.Domain/` following the folder layout above. No new projects are created for this feature — domain types are added to the existing `Vendor.Domain` class library.

---

## Complexity Tracking

No constitution violations. The only non-trivial pattern is `AggregateRoot<TId>` with a `_domainEvents` collection — this is mandated by Principle VI and has no simpler alternative given the outbox requirement.

---

## Post-Phase 1 Constitution Re-check

*Performed after design artifacts (research.md, data-model.md, contracts/) are complete.*

| Principle | Design Verification | Status |
|-----------|---------------------|--------|
| I — Zero Domain NuGet deps | All 11 aggregates, 6 VOs, 22 events, 16 interfaces use BCL only (`System`, `System.Collections.Generic`, `System.Text.RegularExpressions`). No package references added. | ✅ PASS |
| II — Result-Oriented Handlers | Domain aggregates throw domain exceptions (correct). Application-layer handlers (future feature) return `Result<T>`. No Application code in this feature. | ✅ PASS |
| III — Owned Types | `Money`, `Address`, `DateRange`, `Slug`, `Weight`, `Dimensions` documented as owned types in data-model.md. EF Core mapping will be in `Vendor.Infrastructure` — confirmed no separate tables. | ✅ PASS |
| IV — Clone-Per-Vendor | `Cart.maxItems`, `ProductVariant.LowStockThreshold`, `Promotion.MaxDiscountAmount` are injected as parameters or read from `VendorSettings` — no hardcoded vendor-specific values. | ✅ PASS |
| V — Secrets | Domain layer contains zero secret handling. `ISecretResolver` interface is declared (port), implemented in Infrastructure. | ✅ PASS |
| VI — Outbox | All aggregates use `RaiseDomainEvent()` → events collected on aggregate, enqueued to `OutboxMessages` by Infrastructure interceptor. No MediatR dispatch inside transactions. | ✅ PASS |
| VII — ≥90% Domain Coverage | 15 validation scenarios covering all invariant paths documented in quickstart.md. All testable with pure xUnit, zero infrastructure. | ✅ PASS |

**Post-design gate result: ALL PASS — proceed to `/speckit-tasks`.**
