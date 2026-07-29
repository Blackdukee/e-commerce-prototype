# Tasks: Idempotent Payment Ledger

**Input**: Design documents from `/specs/008-idempotent-payment-ledger/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/payment-endpoints.md, quickstart.md

**Tests**: Unit, integration, and API contract test tasks are included to satisfy Constitution Rule VII coverage targets.

**Organization**: Tasks are grouped by user story (US1, US2, US3) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and domain enum setup

- [x] T001 Verify project structure and reference paths for Payment domain module in `src/Vendor.Domain/Aggregates/Payment/`
- [x] T002 [P] Create domain enums in `src/Vendor.Domain/Aggregates/Payment/Enums/IdempotencyStatus.cs` and `src/Vendor.Domain/Aggregates/Payment/Enums/PaymentLedgerEventType.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core interfaces and memory locking infrastructure that MUST be complete before user story handlers can be built

**⚠️ CRITICAL**: No user story command handler work can begin until this phase is complete

- [x] T003 [P] Create repository interface IPaymentIdempotencyRepository in `src/Vendor.Domain/Interfaces/IPaymentIdempotencyRepository.cs`
- [x] T004 [P] Create repository interface IPaymentLedgerRepository in `src/Vendor.Domain/Interfaces/IPaymentLedgerRepository.cs`
- [x] T005 [P] Create repository interface IWebhookEventRepository in `src/Vendor.Domain/Interfaces/IWebhookEventRepository.cs`
- [x] T006 Implement InMemoryIdempotencyLockManager in `src/Vendor.Infrastructure/Payments/Concurrency/InMemoryIdempotencyLockManager.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Protection Against Duplicate Payment Processing (Priority: P1) 🎯 MVP

**Goal**: Shield payment endpoints with client-generated UUID v4 idempotency keys, storing request hashes and caching response payloads to return cached results on duplicate submissions without re-charging buyers.

**Independent Test**: Submit a payment request with a unique idempotency key UUID, then resubmit the exact same request with the same UUID key. Verify that only a single payment authorization/capture occurs while returning the cached response.

### Tests for User Story 1

- [x] T007 [P] [US1] Unit test PaymentIdempotencyKey entity and payload hash matching in `tests/Vendor.Domain.Tests/Payment/PaymentIdempotencyKeyTests.cs`
- [x] T008 [P] [US1] Unit test IdempotencyBehavior pipeline behavior in `tests/Vendor.Application.Tests/Payments/IdempotencyBehaviorTests.cs`

### Implementation for User Story 1

- [x] T009 [P] [US1] Create PaymentIdempotencyKey entity in `src/Vendor.Domain/Aggregates/Payment/PaymentIdempotencyKey.cs`
- [x] T010 [P] [US1] Create EF Core entity configuration PaymentIdempotencyKeyConfiguration in `src/Vendor.Infrastructure/Persistence/Configurations/PaymentIdempotencyKeyConfiguration.cs`
- [x] T011 [US1] Implement PaymentIdempotencyRepository in `src/Vendor.Infrastructure/Persistence/Repositories/PaymentIdempotencyRepository.cs`
- [x] T012 [US1] Create IdempotencyBehavior pipeline behavior in `src/Vendor.Application/Common/Behaviors/IdempotencyBehavior.cs`
- [x] T013 [US1] Implement ProcessPaymentCommand and Handler in `src/Vendor.Application/Commands/Payments/ProcessPayment/ProcessPaymentCommand.cs`
- [x] T014 [US1] Map POST /api/v1/payments/process endpoint in `src/Vendor.Api/Endpoints/PaymentEndpoints.cs`
- [x] T015 [US1] Integration test idempotency endpoint replay and payload mismatch rejection in `tests/Vendor.Api.Tests/Payments/ProcessPaymentIdempotencyTests.cs`

**Checkpoint**: User Story 1 (MVP) fully functional and independently testable

---

## Phase 4: User Story 2 - Immutable Financial Audit Trail and Status Timeline (Priority: P2)

**Goal**: Track every payment state transition (Intent, Authorized, Captured, Refunded, Failed) as a brand-new immutable row with sequence numbers, preventing SQL UPDATE/DELETE queries.

**Independent Test**: Progress a payment through intent creation, authorization, capture, and refund. Query GET /api/v1/payments/{paymentId}/ledger and verify that 4 distinct sequential entries exist without modifying historical rows.

### Tests for User Story 2

- [x] T016 [P] [US2] Unit test PaymentLedgerEntry entity invariants and sequence numbering in `tests/Vendor.Domain.Tests/Payment/PaymentLedgerEntryTests.cs`
- [x] T017 [P] [US2] Unit test GetPaymentLedgerQueryHandler in `tests/Vendor.Application.Tests/Payments/GetPaymentLedgerQueryHandlerTests.cs`

### Implementation for User Story 2

- [x] T018 [P] [US2] Create PaymentLedgerEntry aggregate entity in `src/Vendor.Domain/Aggregates/Payment/PaymentLedgerEntry.cs`
- [x] T019 [P] [US2] Create domain event classes PaymentCapturedEvent, PaymentRefundedEvent, PaymentFailedEvent in `src/Vendor.Domain/Aggregates/Payment/Events/PaymentEvents.cs`
- [x] T020 [P] [US2] Create EF Core configuration PaymentLedgerEntryConfiguration in `src/Vendor.Infrastructure/Persistence/Configurations/PaymentLedgerEntryConfiguration.cs`
- [x] T021 [US2] Implement PaymentLedgerRepository in `src/Vendor.Infrastructure/Persistence/Repositories/PaymentLedgerRepository.cs`
- [x] T022 [US2] Implement intent write in ProcessPaymentCommandHandler in `src/Vendor.Application/Commands/Payments/ProcessPayment/ProcessPaymentCommandHandler.cs`
- [x] T023 [US2] Implement GetPaymentLedgerQuery and Handler in `src/Vendor.Application/Queries/Payments/GetPaymentLedger/GetPaymentLedgerQuery.cs`
- [x] T024 [US2] Map GET /api/v1/payments/{paymentId}/ledger endpoint in `src/Vendor.Api/Endpoints/PaymentEndpoints.cs`
- [x] T025 [US2] Integration test immutable ledger timeline querying in `tests/Vendor.Api.Tests/Payments/PaymentLedgerTimelineTests.cs`

**Checkpoint**: User Stories 1 AND 2 fully functional and independently testable

---

## Phase 5: User Story 3 - Secure Asynchronous Webhook Event Ingestion (Priority: P3)

**Goal**: Validate cryptographic signatures on incoming payment provider webhooks, deduplicate events using event IDs, and append new status entries to the ledger with Polly backoff retries for early-arriving webhooks.

**Independent Test**: Send valid signed webhooks, tampered webhooks, and duplicate event IDs to POST /api/v1/payments/webhooks/{providerName}. Verify valid events append new timeline rows, invalid signatures return 401, and duplicate event IDs return 200 without appending duplicate rows.

### Tests for User Story 3

- [x] T026 [P] [US3] Unit test WebhookEventEntry deduplication in `tests/Vendor.Domain.Tests/Payment/WebhookEventEntryTests.cs`
- [x] T027 [P] [US3] Unit test ProcessWebhookCommandHandler with signature verification and retry backoff in `tests/Vendor.Application.Tests/Payments/ProcessWebhookCommandHandlerTests.cs`

### Implementation for User Story 3

- [x] T028 [P] [US3] Create WebhookEventEntry entity in `src/Vendor.Domain/Aggregates/Payment/WebhookEventEntry.cs`
- [x] T029 [P] [US3] Create EF Core entity configuration WebhookEventEntryConfiguration in `src/Vendor.Infrastructure/Persistence/Configurations/WebhookEventEntryConfiguration.cs`
- [x] T030 [US3] Implement WebhookEventRepository in `src/Vendor.Infrastructure/Persistence/Repositories/WebhookEventRepository.cs`
- [x] T031 [US3] Add VerifyWebhookSignatureAsync to IPaymentGateway adapters in `src/Vendor.Infrastructure/Payments/Gateways/StripePaymentGateway.cs`
- [x] T032 [US3] Implement ProcessWebhookCommand and Handler with Polly backoff retry in `src/Vendor.Application/Commands/Payments/ProcessWebhook/ProcessWebhookCommand.cs`
- [x] T033 [US3] Map POST /api/v1/payments/webhooks/{providerName} endpoint in `src/Vendor.Api/Endpoints/PaymentEndpoints.cs`
- [x] T034 [US3] Integration test webhook signature verification and deduplication in `tests/Vendor.Api.Tests/Payments/WebhookIngestionTests.cs`

**Checkpoint**: All user stories fully functional and independently testable

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Database migrations, swagger documentation, and end-to-end quickstart validation

- [x] T035 [P] Add EF Core migration for PaymentIdempotencyKeys, PaymentLedgerEntries, and WebhookEventEntries in `src/Vendor.Infrastructure/Persistence/Migrations/`
- [x] T036 Update OpenAPI swagger documentation for Payment endpoints in `src/Vendor.Api/Endpoints/PaymentEndpoints.cs`
- [x] T037 Execute quickstart.md validation scenarios and verify layer test coverage targets across Domain, Application, Infrastructure, and API projects

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User Story 1 (P1 - MVP) -> User Story 2 (P2) -> User Story 3 (P3)
- **Polish (Phase 6)**: Depends on all user stories being complete
