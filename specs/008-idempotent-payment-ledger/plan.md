# Implementation Plan: Idempotent Payment Ledger

**Branch**: `008-idempotent-payment-ledger` | **Date**: 2026-07-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-idempotent-payment-ledger/spec.md`

## Summary

Implement client-generated UUID v4 idempotency key shielding, an append-only immutable financial payment ledger, and cryptographically verified webhook event ingestion. The design writes payment intents to the ledger before external gateway API dispatch, caches HTTP responses against idempotency keys to safely handle network timeouts/retries, and ingests signed webhook status updates without mutating past database rows.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`)

**Primary Dependencies**: MediatR 12.x, FluentValidation 11.x, EF Core 9.x (`Microsoft.EntityFrameworkCore.SqlServer`), Polly (for webhook backoff retries)

**Storage**: Microsoft SQL Server (MSSQL) via EF Core Code-First Migrations

**Testing**: xUnit, Moq/NSubstitute, FluentAssertions, `WebApplicationFactory<Program>` (API integration tests)

**Target Platform**: Cross-platform Linux / Windows server deployment (.NET 9 LTS runtime)

**Project Type**: REST API Web Service / Clean Architecture Backend

**Performance Goals**: Idempotency key lookup & intent insertion < 50ms; Webhook deduplication check < 50ms; In-flight lock timeout 10s.

**Constraints**: Clean Architecture dependency rules (Domain has zero external NuGet dependencies), append-only ledger entries (zero SQL UPDATE/DELETE), reference-only secrets, transactional outbox for domain events.

**Scale/Scope**: High-concurrency checkout protection, 100% auditable payment state history.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Compliance Details |
|-----------|--------|-------------------|
| **I. Clean Architecture** | ✅ PASS | All domain aggregates (`PaymentIdempotencyKey`, `PaymentLedgerEntry`, `WebhookEventEntry`) reside in `Vendor.Domain` with zero external NuGet packages. Interfaces in Application; concrete EF Core mappings & gateway adapters in Infrastructure; endpoints in API. |
| **II. Result-Oriented Control Flow** | ✅ PASS | Payment commands and queries return `Result<T>` or `Result.ValidationFailure` / `Result.Failure`. Business exceptions are not thrown. |
| **III. MSSQL via EF Core & Owned Types** | ✅ PASS | Database is MSSQL. Money value objects mapped as owned types. Dedicated `IEntityTypeConfiguration<T>` classes created in Infrastructure. |
| **IV. Clone-Per-Vendor Isolation** | ✅ PASS | Vendor gateway configuration, secrets, and provider choices are loaded strictly via `vendor.config.json`. Zero C# code changes per vendor. |
| **V. Secrets Management** | ✅ PASS | Webhook signature keys and API secrets use `ref:env:` references resolved at runtime by `ISecretResolver`. No raw secrets committed. |
| **VI. Domain Events via Outbox** | ✅ PASS | Payment state changes write domain events (`PaymentCapturedEvent`, `PaymentRefundedEvent`) into `OutboxMessages` within the same `SaveChangesAsync` transaction. |
| **VII. Test Coverage Targets** | ✅ PASS | Standard layer thresholds targeted (Domain 90%, Application 85%, Infrastructure 70%, API 75%). |

## Project Structure

### Documentation (this feature)

```text
specs/008-idempotent-payment-ledger/
├── plan.md              # Implementation Plan
├── research.md          # Phase 0 Architectural Decisions & Research
├── data-model.md        # Phase 1 Domain Entity & Database Schema Specifications
├── quickstart.md        # Phase 1 Verification & End-to-End Test Guide
└── contracts/
    └── payment-endpoints.md # Phase 1 REST Endpoint Contracts & Payloads
```

### Source Code Layout

```text
src/
├── Vendor.Domain/
│   ├── Aggregates/
│   │   └── Payment/
│   │       ├── PaymentIdempotencyKey.cs
│   │       ├── PaymentLedgerEntry.cs
│   │       ├── WebhookEventEntry.cs
│   │       ├── Enums/
│   │       │   ├── IdempotencyStatus.cs
│   │       │   └── PaymentLedgerEventType.cs
│   │       └── Events/
│   │           ├── PaymentCapturedEvent.cs
│   │           ├── PaymentRefundedEvent.cs
│   │           └── PaymentFailedEvent.cs
│   └── Interfaces/
│       ├── IPaymentIdempotencyRepository.cs
│       ├── IPaymentLedgerRepository.cs
│       └── IWebhookEventRepository.cs
│
├── Vendor.Application/
│   ├── Commands/
│   │   └── Payments/
│   │       ├── ProcessPayment/
│   │       │   ├── ProcessPaymentCommand.cs
│   │       │   └── ProcessPaymentCommandHandler.cs
│   │       └── ProcessWebhook/
│   │           ├── ProcessWebhookCommand.cs
│   │           └── ProcessWebhookCommandHandler.cs
│   ├── Queries/
│   │   └── Payments/
│   │       └── GetPaymentLedger/
│   │           ├── GetPaymentLedgerQuery.cs
│   │           └── GetPaymentLedgerQueryHandler.cs
│   └── Behaviors/
│       └── IdempotencyBehavior.cs
│
├── Vendor.Infrastructure/
│   ├── Persistence/
│   │   ├── Configurations/
│   │   │   ├── PaymentIdempotencyKeyConfiguration.cs
│   │   │   ├── PaymentLedgerEntryConfiguration.cs
│   │   │   └── WebhookEventEntryConfiguration.cs
│   │   └── Repositories/
│   │       ├── PaymentIdempotencyRepository.cs
│   │       ├── PaymentLedgerRepository.cs
│   │       └── WebhookEventRepository.cs
│   └── Payments/
│       ├── Concurrency/
│       │   └── InMemoryIdempotencyLockManager.cs
│       └── Gateways/
│           ├── StripePaymentGateway.cs
│           └── PayPalPaymentGateway.cs
│
└── Vendor.Api/
    └── Endpoints/
        └── PaymentEndpoints.cs

tests/
├── Vendor.Domain.Tests/
│   └── Payment/
├── Vendor.Application.Tests/
│   └── Payments/
├── Vendor.Infrastructure.Tests/
│   └── Payments/
└── Vendor.Api.Tests/
    └── Payments/
```

**Structure Decision**: Clean Architecture multi-project solution structure adhering to project guidelines.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *None* | N/A | Fully compliant with all Constitution rules |
