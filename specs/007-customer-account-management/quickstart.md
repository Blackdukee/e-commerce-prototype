# Quickstart Validation Guide: Customer Account Management

**Feature**: 007-customer-account-management
**Date**: 2026-07-26

## Prerequisites

- .NET 9 SDK installed
- LocalDB or SQL Server running
- API application built via `dotnet build Vendor.slnx`

---

## Test Suite Execution

Run unit and integration tests covering domain invariants, command handlers, and endpoint security:

```powershell
# Run full solution test suite
dotnet test Vendor.slnx

# Run domain tests specific to Customer aggregate
dotnet test tests/Vendor.Domain.Tests --filter "FullyQualifiedName~Customer"

# Run application handler tests
dotnet test tests/Vendor.Application.Tests --filter "FullyQualifiedName~Customer"

# Run API endpoint integration tests
dotnet test tests/Vendor.Api.Tests --filter "FullyQualifiedName~Customer"
```

---

## End-to-End Manual / Verification Scenarios

### Scenario 1: Account Suspension & Token Revocation

1. Obtain a valid JWT for an active Customer account (`CustomerA`).
2. As an Admin, execute `POST /api/v1/admin/customers/{CustomerA_ID}/suspend` with `{"reason": "Terms violation"}`.
3. **Verify**: Response is `200 OK`. Customer status is `Suspended` in database, `CustomerSuspendedEvent` is written to `OutboxMessages`, and all refresh tokens for `CustomerA` are revoked in the database.

### Scenario 2: Blocked Login & Checkout for Suspended Customer

1. Attempt login with `POST /api/v1/auth/login` using `CustomerA` credentials.
2. **Verify**: Request is rejected with `403 Forbidden` / `ACCOUNT_SUSPENDED`.
3. Attempt placing an order with `POST /api/v1/orders` using `CustomerA` bearer token.
4. **Verify**: Request is rejected with `403 Forbidden` / `ACCOUNT_SUSPENDED`.

### Scenario 3: Idempotent Suspension & Reactivation

1. As an Admin, execute `POST /api/v1/admin/customers/{CustomerA_ID}/suspend` a second time.
2. **Verify**: Response is `200 OK` (idempotent, no duplicate outbox events or errors).
3. As an Admin, execute `POST /api/v1/admin/customers/{CustomerA_ID}/reactivate`.
4. **Verify**: Customer status returns to `Active`, `CustomerReactivatedEvent` is emitted, and `CustomerA` can successfully log in and place orders again.

### Scenario 4: SuperAdmin-Only Promote & Demote Enforcement

1. As a regular `Admin` caller, attempt `POST /api/v1/admin/customers/{CustomerB_ID}/promote`.
2. **Verify**: Rejected with `403 Forbidden` (enforced at both API level and command handler level).
3. As a `SuperAdmin` caller, execute `POST /api/v1/admin/customers/{CustomerB_ID}/promote`.
4. **Verify**: Response is `200 OK`. `CustomerB` role becomes `Admin`, and `CustomerRoleChangedEvent` is written to the outbox.

### Scenario 5: Prevention of Self-Demotion & SuperAdmin Assignment

1. As a `SuperAdmin` caller, attempt `POST /api/v1/admin/customers/{SuperAdmin_ID}/demote`.
2. **Verify**: Rejected with `400 Bad Request` (`SelfModificationNotAllowed`).
3. Attempt to pass `"role": "SuperAdmin"` in any promote or update endpoint.
4. **Verify**: Request is rejected; `SuperAdmin` role is unassignable via API commands.

### Scenario 6: Rate Limit Verification on Promote/Demote

1. Send 11 rapid requests to `POST /api/v1/admin/customers/{id}/promote`.
2. **Verify**: The 11th request is throttled with `429 Too Many Requests` per the tight `"auth"` rate-limiting policy.
