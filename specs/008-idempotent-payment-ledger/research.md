# Phase 0 Research: Idempotent Payment Ledger

**Feature Branch**: `008-idempotent-payment-ledger`
**Date**: 2026-07-29

This document outlines the architectural research, design choices, and technical trade-offs evaluated for implementing client-enforced idempotency keys, an append-only immutable financial ledger, and secure webhook ingestion.

---

## 1. Idempotency Key Persistence & In-Flight Concurrency Control

### Decision
Implement a dedicated `PaymentIdempotencyKey` entity inside `Vendor.Domain` persisted via EF Core to an `IdempotencyKeys` MSSQL table. Concurrency is managed by combining a unique DB index on `KeyUuid` with an in-memory/distributed lock key manager (`IIdempotencyLockManager`) that serializes concurrent requests using the exact same UUID for up to 10 seconds.

### Rationale
- **Persistence before Gateway Dispatch**: Persisting the key status as `Processing` alongside the request payload hash *before* calling external payment gateway APIs guarantees that duplicate retries during network timeouts are intercepted early.
- **Request Payload Hash Validation**: Storing a cryptographic hash (SHA256) of the initial request payload allows instant detection of payload tampering or invalid key reuse (returning `HTTP 422 Unprocessable Entity`).
- **In-Flight Lock**: When a duplicate request arrives while the primary request status is `Processing`, the lock manager suspends the duplicate request for up to 10 seconds waiting for the status to transition to `Completed` or `Failed`, avoiding unnecessary error responses to buyers.

### Alternatives Considered
- **Redis Cache-Only Storage**: Evaluated storing idempotency tokens in Redis. *Rejected* because Redis is optional in vendor deployments (single-instance uses `IMemoryCache`), and payment idempotency records require strict MSSQL persistence for auditability.
- **Database Row Locking (`SELECT FOR UPDATE`)**: Evaluated holding open DB transaction locks while awaiting gateway responses. *Rejected* because external API network latency could exhaust MSSQL connection pool threads.

---

## 2. Immutable Payment Ledger Design

### Decision
Model payment history as an append-only timeline using a `PaymentLedgerEntry` aggregate inside `Vendor.Domain`. Each entry records a discrete state transition (`Intent`, `Authorized`, `Captured`, `Refunded`, `Failed`) with a strictly increasing `SequenceNumber` per `PaymentId`.

### Rationale
- **Zero Mutation**: The DB context configuration (`PaymentLedgerEntryConfiguration`) explicitly disallows state updates or deletions. All state changes are appended as new rows.
- **Domain Event Integration**: Appending a new ledger entry raises corresponding domain events (`PaymentCapturedEvent`, `PaymentRefundedEvent`, etc.) which are persisted into the `OutboxMessages` table in the exact same `SaveChangesAsync` transaction (complying with Constitution Rule VI).
- **Auditability**: Finance teams can query the complete timeline for any payment by sorting entries by `SequenceNumber` or `TimestampUtc`.

### Alternatives Considered
- **Mutating Aggregate Root State Column**: Evaluated standard state updates on a `Payments` table column. *Rejected* because FR-006 & FR-007 explicitly forbid updating database rows to change payment status.
- **Full Event Sourcing (Marten / EventStoreDB)**: Evaluated adopting event sourcing for the entire domain. *Rejected* because it introduces external NuGet dependencies to the Domain layer (violating Constitution Rule I) and adds unnecessary architectural complexity.

---

## 3. Webhook Signature Verification & Idempotent Ingestion

### Decision
Extend the `IPaymentGateway` interface in `Vendor.Domain` with a `VerifyWebhookSignatureAsync` method implemented in `Vendor.Infrastructure` adapters (Stripe, PayPal, Paymob). Incoming webhooks are tracked in a `WebhookEventEntry` table keyed by `(GatewayName, EventId)`. If an incoming event references a payment intent that is not yet visible in the database, the handler executes a Polly retry policy (3 retries with exponential backoff over 5 seconds).

### Rationale
- **Security First**: Signatures are cryptographically validated against vendor secrets resolved via `ISecretResolver` prior to payload parsing. Invalid signatures fail immediately with `HTTP 401 Unauthorized`.
- **Deduplication**: Duplicate webhook event dispatches from gateway retries hit the unique constraint on `(GatewayName, EventId)` and return `HTTP 200 OK` instantly without creating duplicate ledger entries.
- **Transient Commit Window Handling**: Polling retries gracefully bridge the race condition where a gateway webhook reaches the API faster than the local intent transaction finishes committing.

### Alternatives Considered
- **Queueing Webhooks to External Message Broker**: Evaluated routing incoming webhooks through RabbitMQ or AWS SQS. *Rejected* to maintain clone-per-vendor simplicity without mandating external message broker infrastructure.
