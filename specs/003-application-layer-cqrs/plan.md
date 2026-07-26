# Implementation Plan: Application Layer CQRS & Pipeline Architecture

**Branch**: `003-application-layer-cqrs` | **Date**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

---

## Summary

Build the `Vendor.Application` project on top of `Vendor.Domain`, implementing ~35 commands and ~15 queries across 11 modules (Auth, Products, Customers, Cart, Orders, Payments, Shipments, Promotions, Returns, Analytics, VendorSettings). 

All handlers return `Result<T>` or `Result` and never throw for business failures. A 5-stage MediatR pipeline (`Logging` → `Validation` → `Idempotency` → `Transaction` → `Performance`) enforces cross-cutting concerns. Two orchestration flows (`CheckoutOrderCommand` and `Return/Exchange` workflow) coordinate multi-aggregate transaction boundaries. 7 application interfaces (`IUnitOfWork`, `IIdempotencyStore`, `ICacheService`, `ICurrentUserService`, `ITokenService`, `IExternalAuthService`, `IDateTimeProvider`) decouple application logic from infrastructure.

---

## Technical Context

**Language/Version**: C# 12 / .NET 9 (`net9.0`)

**Primary Dependencies (Application)**: 
- `Vendor.Domain` (project reference)
- `MediatR` 12.x (mediator and pipeline behaviors)
- `FluentValidation` 11.x (input validation rule chains)

**Forbidden Dependencies (Application)**: EF Core, Microsoft.Data.SqlClient, HTTP Client SDKs, Redis SDKs, AWS SDKs, Vault SDKs.

**Storage Abstraction**: `IUnitOfWork` for transaction boundaries; repository interfaces (`IProductRepository`, `IOrderRepository`, etc.) from `Vendor.Domain`.

**Testing**:
- `Vendor.Application.Tests` → xUnit + NSubstitute / in-memory fakes for repository & adapter interfaces. 85%+ line coverage target.

**Target Platform**: .NET 9 class library (`Vendor.Application.csproj`).

**Performance Goals**: Handler execution (excluding external network/DB calls) < 5ms. Pipeline overhead < 1ms. Warnings logged for total request duration > 500ms.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Check | Status |
|-----------|-------|--------|
| I — Clean Architecture | `Vendor.Application` depends ONLY on `Vendor.Domain` + MediatR/FluentValidation abstractions. Zero infrastructure package references. | ✅ PASS |
| II — Result-Oriented Handlers | 100% of handlers return `Result<T>` or `Result`. Pipeline catches exceptions at boundaries and converts them into `Result.Failure()`. No business logic throws exceptions. | ✅ PASS |
| III — EF Core Owned Types | Application layer passes domain VOs (`Money`, `Address`) directly to aggregates. Persistence mapping details stay in Infrastructure. | ✅ PASS |
| IV — Clone-Per-Vendor | All vendor configurations read via `GetVendorConfigQuery` / `ICurrentUserService.VendorId` — zero hardcoded vendor conditionals. | ✅ PASS |
| V — Secrets Management | Secret reference resolution delegated to `ISecretResolver` (Domain port). Application never accesses raw secrets. | ✅ PASS |
| VI — Domain Events via Outbox | Handlers mutate aggregates and call `IUnitOfWork.CommitAsync()`. EF Core interceptor enqueues raised domain events into outbox in same transaction. | ✅ PASS |
| VII — Test Coverage ≥ 85% Application | Unit tests for all 50 handlers + 5 pipeline behaviors using repository/adapter fakes. | ✅ PASS |

**Gate result: ALL PASS — proceed to Phase 0.**

---

## Project Structure

### Documentation (this feature)

```text
specs/003-application-layer-cqrs/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── application-result-taxonomy.md
│   └── command-query-inventory.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/
├── Vendor.Application/                   # ← THIS FEATURE
│   ├── Common/
│   │   ├── Results/
│   │   │   ├── Result.cs
│   │   │   ├── ResultT.cs
│   │   │   └── Error.cs
│   │   ├── Behaviors/
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── IdempotencyBehavior.cs
│   │   │   ├── TransactionBehavior.cs
│   │   │   └── PerformanceBehavior.cs
│   │   └── Messaging/
│   │       ├── ICommand.cs
│   │       ├── IQuery.cs
│   │       └── IIdempotentRequest.cs
│   ├── Interfaces/
│   │   ├── IUnitOfWork.cs
│   │   ├── IIdempotencyStore.cs
│   │   ├── ICacheService.cs
│   │   ├── ICurrentUserService.cs
│   │   ├── ITokenService.cs
│   │   ├── IExternalAuthService.cs
│   │   └── IDateTimeProvider.cs
│   ├── Modules/
│   │   ├── Auth/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   ├── Dtos/
│   │   │   └── Validators/
│   │   ├── Products/
│   │   ├── Customers/
│   │   ├── Cart/
│   │   ├── Orders/
│   │   ├── Payments/
│   │   ├── Shipments/
│   │   ├── Promotions/
│   │   ├── Returns/
│   │   ├── Analytics/
│   │   └── VendorSettings/
│   └── DependencyInjection.cs
│
├── Vendor.Domain/                        # (Completed in Feature 002)
├── Vendor.Infrastructure/                # Next feature
└── Vendor.Api/                           # Next feature

tests/
├── Vendor.Application.Tests/             # Unit tests, ≥85% coverage
│   ├── Common/
│   │   ├── ResultTests.cs
│   │   └── PipelineBehaviorTests.cs
│   └── Modules/
│       ├── AuthTests.cs
│       ├── CheckoutOrchestrationTests.cs
│       └── ReturnWorkflowTests.cs
```

---

## Complexity Tracking

No constitution violations. `Result<T>` and 5-stage MediatR pipeline are explicitly mandated by Principles II and VI.

---

## Post-Phase 1 Constitution Re-check

*Performed after design artifacts (research.md, data-model.md, contracts/) are complete.*

| Principle | Design Verification | Status |
|-----------|---------------------|--------|
| I — Clean Architecture | `Vendor.Application` depends ONLY on `Vendor.Domain` + MediatR 12 / FluentValidation 11 abstractions. Zero infrastructure package references added. | ✅ PASS |
| II — Result-Oriented Handlers | `Result<T>` and `Error` variants (`NotFoundError`, `ValidationError`, `ConflictError`, etc.) defined. Handlers return `Result<T>`, pipeline short-circuits validation errors as 422. | ✅ PASS |
| III — EF Core Owned Types | Application DTOs pass domain value objects (`Money`, `Address`, `DateRange`) directly to/from aggregate methods. Storage details remain in Infrastructure. | ✅ PASS |
| IV — Clone-Per-Vendor | Multi-tenant vendor identity and settings read via `ICurrentUserService.VendorId` and `GetVendorConfigQuery` — zero hardcoded vendor conditionals. | ✅ PASS |
| V — Secrets | Secrets referenced via `ISecretResolver` (Domain port); application handlers never handle raw secrets. | ✅ PASS |
| VI — Outbox | Handlers mutate aggregates and call `IUnitOfWork.CommitAsync()`. Transaction behavior wraps writes in atomic transactions. Outbox interceptor handles dispatch. | ✅ PASS |
| VII — ≥85% Coverage Target | Unit test strategy for all 50 handlers + 5 pipeline behaviors using repository/adapter fakes documented in quickstart.md. | ✅ PASS |

**Post-design gate result: ALL PASS — proceed to `/speckit-tasks`.**
