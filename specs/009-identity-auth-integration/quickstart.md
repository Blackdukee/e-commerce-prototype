# Quickstart Validation Guide: Identity Auth Integration

**Feature**: `009-identity-auth-integration`
**Date**: 2026-07-29

This quickstart guide details the runnable test scenarios to validate ASP.NET Core Identity authentication integration.

---

## 1. Unit & Integration Test Suites

Run solution tests using `dotnet test`:

```bash
dotnet test --filter "Category=Auth|FullyQualifiedName~Identity"
```

---

## 2. Validation Scenarios

### Scenario A: Atomic Registration & Password Sign-In
1. Submit `POST /api/v1/auth/register` with new credentials.
2. Verify HTTP `201 Created` response containing JWT token pair.
3. Query database to confirm `AspNetUsers` and `Customers` records share matching `CustomerId` foreign key.
4. Submit `POST /api/v1/auth/login` with correct password to verify `200 OK` JWT token issuance.

### Scenario B: Lockout Counter Enforcement
1. Submit 5 consecutive requests to `POST /api/v1/auth/login` with an incorrect password.
2. Verify that the 5th attempt returns lockout failure status (`HTTP 423 Locked Out`).
3. Verify `LockoutEnd` timestamp in `AspNetUsers` is set 15 minutes into the future.

### Scenario C: Google OAuth Login & Unverified Email Protection
1. Post a valid Google ID token for an unverified email address matching an existing account to `POST /api/v1/auth/external/google`.
2. Verify server rejects request with `HTTP 409 Conflict`.
3. Post a valid Google ID token for a new email address.
4. Verify HTTP `200 OK` response and confirm that `ApplicationUser` and `Customer` aggregate are created atomically in a single transaction and linked via `AspNetUserLogins`.
