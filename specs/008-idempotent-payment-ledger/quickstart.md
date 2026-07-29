# Quickstart & Verification Guide: Idempotent Payment Ledger

**Feature Branch**: `008-idempotent-payment-ledger`
**Date**: 2026-07-29

This guide outlines end-to-end verification scenarios to validate that client-generated idempotency keys, append-only payment ledgers, and signed webhook deduplication function correctly.

---

## 1. Automated Test Suite Execution

Run the complete test suite across Domain, Application, Infrastructure, and API layers:

```powershell
# Run unit tests for domain invariants and application commands
dotnet test tests/Vendor.Domain.Tests --filter "Category=Payment"
dotnet test tests/Vendor.Application.Tests --filter "Category=Payment"

# Run integration and API contract tests
dotnet test tests/Vendor.Infrastructure.Tests --filter "Category=Payment"
dotnet test tests/Vendor.Api.Tests --filter "Category=Payment"
```

---

## 2. Manual End-to-End Verification Scenarios

### Scenario A: Verify Idempotency Shielding (Duplicate Request Replay)
1. Generate a random UUID v4 string (e.g. `9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d`).
2. Dispatch `POST /api/v1/payments/process` with `Header Idempotency-Key: 9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d`.
3. Confirm response is `201 Created` with a new `paymentId`.
4. Immediately re-send the exact same HTTP request with the identical `Idempotency-Key` header.
5. **Expected Outcome**: The server returns `200 OK` (or `201 Created`) in under 50ms with the cached JSON response. Inspect payment provider logs to verify **zero duplicate external API charges occurred**.

---

### Scenario B: Verify Payload Mismatch Rejection
1. Send a request with `Idempotency-Key: 9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d` but change the amount from `149.99` to `299.99`.
2. **Expected Outcome**: The server rejects the request immediately with `HTTP 422 Unprocessable Entity` stating that request payload parameters differ from the original key registration.

---

### Scenario C: Verify Immutable Append-Only Ledger History
1. Perform a payment checkout flow.
2. Query `GET /api/v1/payments/{paymentId}/ledger`.
3. Inspect returned timeline array.
4. **Expected Outcome**:
   - `Sequence 1`: Event `Intent`
   - `Sequence 2`: Event `Authorized`
   - `Sequence 3`: Event `Captured`
   - All historical entries retain their original `createdAtUtc` timestamps and sequence numbers. Database inspection confirms **zero SQL UPDATE queries executed on `PaymentLedgerEntries`**.

---

### Scenario D: Verify Signed Webhook Ingestion & Event Deduplication
1. Send a webhook payload to `POST /api/v1/payments/webhooks/Stripe` with a valid signature and unique `eventId` (`evt_test_100`).
2. Confirm response is `200 OK` and a new ledger timeline entry is appended.
3. Re-send the exact same webhook payload with `evt_test_100`.
4. **Expected Outcome**: Server returns `200 OK` instantly, skipping duplicate ledger creation.
5. Send a webhook payload with a tampered/invalid signature.
6. **Expected Outcome**: Server returns `401 Unauthorized` without modifying any DB tables.
