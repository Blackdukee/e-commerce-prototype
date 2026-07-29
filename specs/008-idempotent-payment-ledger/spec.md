# Feature Specification: Idempotent Payment Ledger

**Feature Branch**: `008-idempotent-payment-ledger`

**Created**: 2026-07-29

**Status**: Draft

**Input**: User description: "Basic payment setups fail during network timeouts. block catastrophic duplicate charges, enforce client-generated idempotency keys (UUIDs). The server saves this key first; if a duplicate request arrives, it immediately returns the cached result instead of re-charging the buyer. Never overwrite database rows to update payment statuses. Instead, implement an immutable ledger where every state change, authorized, captured, or refunded, creates a brand-new row. Securely ingest asynchronous updates using signed webhooks, validating event IDs before appending them to the timeline. This design writes intents to the ledger before calling the API, shields endpoints with idempotency keys, and uses webhooks to build a flawless, fully auditable financial history."

## Clarifications

### Session 2026-07-29

- Q: How should the server handle concurrent duplicate requests specifying an idempotency key that is currently being processed? → A: Lock and wait for primary request completion (up to 10s timeout), returning the cached result once complete.
- Q: If a client submits a request using an existing idempotency key, but the request payload differs from the original request, how should the server respond? → A: Reject immediately with HTTP 422 Unprocessable Entity detailing the payload mismatch.
- Q: If the external payment provider API call times out or fails after the Intent entry has been written to the immutable ledger, how should the system update the payment timeline? → A: Append a new immutable entry with state Failed and error details to the ledger timeline.
- Q: If an asynchronous webhook event arrives before the initial payment intent record is visible in the database, how should the webhook ingestion service handle the event? → A: Retry ingestion with short exponential backoff (3 attempts over 5s) before failing.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Protection Against Duplicate Payment Processing (Priority: P1)

As a buyer completing a checkout during poor network connectivity or client timeouts, I want my payment request to be processed exactly once even if my application retries the request automatically or manually, so that I am never double-charged for a single purchase.

**Why this priority**: Preventing duplicate billing is critical for protecting customer funds, maintaining merchant trust, and avoiding expensive chargeback disputes.

**Independent Test**: Submit a payment request with a unique idempotency key, simulate a client network timeout, and immediately resubmit the identical request with the same idempotency key. Verify that only a single payment authorization/capture is executed with external providers while returning the cached response to the client.

**Acceptance Scenarios**:

1. **Given** a new payment request with a valid client-generated UUID idempotency key, **When** submitted to the payment endpoint for the first time, **Then** the server records the key, writes the payment intent to the immutable ledger, executes the payment, caches the result, and returns success.
2. **Given** a payment request that was previously completed, **When** a duplicate request arrives with the exact same idempotency key, **Then** the server immediately returns the cached response without initiating a new payment charge or calling downstream payment services.
3. **Given** a payment request submitted without an idempotency key or with an invalid non-UUID format, **When** processed by the server, **Then** the request is rejected with an explicit validation error before any payment intent or ledger record is created.

---

### User Story 2 - Immutable Financial Audit Trail and Status Timeline (Priority: P2)

As a store finance administrator or auditor, I want every payment state transition (authorization, capture, partial refund, full refund, failure) to create a new immutable record rather than mutating existing database rows, so that the store maintains a complete, tamper-evident financial history.

**Why this priority**: Immutable ledger records guarantee legal compliance, simple reconciliation, and full traceability across complex payment lifecycles.

**Independent Test**: Progress a payment through intent creation, authorization, capture, and refund. Verify that four distinct chronological entries exist in the ledger timeline and that historical rows remain completely unchanged.

**Acceptance Scenarios**:

1. **Given** a customer initiating a checkout, **When** the payment intent is registered, **Then** an initial intent record is written to the ledger as sequence 1 before external API interaction.
2. **Given** an existing payment intent, **When** an authorization or capture event occurs, **Then** a new record is appended to the payment ledger timeline with the updated status and incremented sequence number, leaving the original intent row untouched.
3. **Given** a financial audit or inquiry, **When** retrieving the history for a specific payment, **Then** the system presents the complete chronological sequence of state transitions from creation to final settlement.

---

### User Story 3 - Secure Asynchronous Webhook Event Ingestion (Priority: P3)

As a system administrator, I want asynchronous payment status updates received via external webhooks to be cryptographically validated and deduplicated using event IDs before modifying the payment timeline, so that unauthorized or duplicate events cannot corrupt financial records.

**Why this priority**: Webhooks handle asynchronous financial updates (such as asynchronous captures or chargebacks); secure, idempotent ingestion prevents out-of-order or spoofed financial state changes.

**Independent Test**: Dispatch signed valid webhooks, tampered webhooks, and duplicate webhook event IDs to the webhook ingestion endpoint. Verify that only valid unique events append new timeline rows and duplicate event IDs return success without appending duplicate rows.

**Acceptance Scenarios**:

1. **Given** an incoming webhook from an external payment gateway, **When** the payload has a valid cryptographic signature and a unique event ID, **Then** the system appends the status update to the payment ledger and logs the processed event ID.
2. **Given** an incoming webhook with an invalid or missing signature, **When** evaluated by the server, **Then** the request is rejected immediately with an unauthorized status without altering any ledger data.
3. **Given** a valid webhook event whose event ID has already been successfully ingested, **When** received again due to gateway redelivery, **Then** the system acknowledges the webhook successfully while skipping duplicate ledger creation.

---

### Edge Cases

- If a duplicate request with an identical idempotency key arrives while the initial payment request is still actively processing (in-flight), the system locks and waits up to 10 seconds for the primary request to complete before returning the cached result.
- If an idempotency key is resubmitted with a request payload that differs from the original request (such as different amount or currency), the system rejects the request with HTTP 422 Unprocessable Entity without re-executing processing.
- If an asynchronous webhook event arrives before the initial payment intent record has finished saving in the database, the system retries ingestion with short exponential backoff (3 attempts over 5 seconds) before failing.
- If the external payment provider API call fails or times out after the intent is written to the ledger, the system appends a new immutable entry with state Failed and error details to the ledger timeline.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST enforce mandatory client-generated UUID v4 idempotency keys for all payment processing endpoints.
- **FR-002**: System MUST persist the idempotency key and initial processing state before initiating any external payment gateway API calls.
- **FR-003**: System MUST cache the final HTTP response status and body against the idempotency key upon completion of payment processing.
- **FR-004**: System MUST detect duplicate idempotency key requests and immediately return the cached response without re-triggering payment gateway calls.
- **FR-005**: System MUST reject requests reusing an existing idempotency key if the request parameters (e.g., amount, currency, buyer identifier) differ from the original request, returning HTTP 422 Unprocessable Entity.
- **FR-006**: System MUST record all payment state changes (Intent, Authorized, Captured, Refunded, Failed) as new immutable rows in a payment ledger.
- **FR-007**: System MUST NEVER execute SQL UPDATE or DELETE operations on payment ledger state records.
- **FR-008**: System MUST write a payment intent row to the ledger prior to calling external payment gateway authorization endpoints.
- **FR-009**: System MUST verify cryptographic signatures on all incoming payment provider webhook notifications prior to payload parsing.
- **FR-010**: System MUST track ingested webhook event IDs and reject duplicate processing of previously processed webhook events.
- **FR-011**: System MUST lock concurrent in-flight requests sharing an identical idempotency key and wait up to 10 seconds for the primary request to complete before returning the cached result.
- **FR-012**: System MUST append a new immutable ledger row with status Failed and error details if an external payment gateway call fails or times out after intent creation.
- **FR-013**: System MUST retry webhook processing using exponential backoff (3 attempts over 5 seconds) if the associated payment intent record is not yet visible in the database.

### Key Entities

- **Payment Idempotency Key**: Tracks unique payment request tokens. Attributes include Key UUID, Request Payload Hash, Status (Processing, Completed, Failed), Response Code, Response Body, and Expiration Timestamp.
- **Payment Ledger Entry**: Represents a single immutable state change in a payment's timeline. Attributes include Payment ID, Sequence Number, Event Type (Intent, Authorized, Captured, Refunded, Failed), Amount, Currency, Gateway Reference ID, Ledger Timestamp, and Correlation ID.
- **Webhook Event Entry**: Records processed external notification events. Attributes include Event ID, Gateway Name, Event Type, Payload Hash, Processing Timestamp, and Verification Result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of duplicate payment submissions with identical idempotency keys return cached responses without executing secondary payment gateway charges.
- **SC-002**: Zero database update or delete operations are executed against historical payment ledger state entries during payment lifecycles.
- **SC-003**: 100% of invalidly signed webhook payloads are rejected before affecting payment ledger records.
- **SC-004**: Duplicate webhook deliveries sharing identical event IDs process in under 50 milliseconds returning idempotent success without appending duplicate ledger rows.
- **SC-005**: Payment intents are successfully recorded in the immutable ledger before downstream gateway dispatch in 100% of payment attempts.

## Assumptions

- Clients generate cryptographically unique RFC 4122 compliant UUID v4 values for idempotency keys.
- Idempotency key cached entries are retained for a minimum of 24 hours.
- External payment provider adapters supply cryptographic signature headers and unique webhook event identifiers.
- Database storage guarantees serializable or transactional consistency for sequence numbering on payment ledger entries.
