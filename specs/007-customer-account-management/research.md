# Research: Customer Account Management

**Feature**: 007-customer-account-management
**Date**: 2026-07-26

## R1: Customer Aggregate Extension Strategy

**Decision**: Extend the existing `Customer` aggregate root in `Vendor.Domain.Aggregates.Customer` with `Role` (`CustomerRole` enum) and `Status` (`CustomerStatus` enum), alongside suspension metadata (`SuspendedAtUtc`, `SuspensionReason`) and role audit metadata (`RoleChangedAtUtc`, `RoleChangedByCustomerId`).

**Rationale**: 
- Extending the existing `Customer` aggregate preserves a unified domain model without introducing unnecessary new aggregates or cross-aggregate synchronization complexity.
- Keeps entity lifecycle, invariants, and transactional outbox events centered around a single aggregate root.
- Complies strictly with user directive: "Extend the existing Customer aggregate and ICustomerRepository rather than introducing a new aggregate... this is additive, not a new architectural layer."

**Alternatives Considered**:
- *Creating a separate `AccountManagement` aggregate*: Rejected because it introduces dual-write complexity, data duplication, and violates the single aggregate root identity principle for customer entity management.

## R2: SuperAdmin Authority & Command-Handler Level Guard

**Decision**: Implement caller authorization checks inside the command handlers (`PromoteCustomerCommandHandler`, `DemoteCustomerCommandHandler`, `SuspendCustomerCommandHandler`, `ReactivateCustomerCommandHandler`) in `Vendor.Application.Modules.Customers`.

**Rationale**:
- Enforcing `SuperAdmin` role validation within the command handler guarantees defence-in-depth: logic cannot be bypassed even if API authorization attributes are misconfigured or bypassed internally.
- `SuperAdmin` role is unassignable via any endpoint or command. It is only assignable as a seed value configured at deploy time in `vendor.config.json` / boot settings.
- SuperAdmin accounts are protected at the aggregate and handler level against self-demotion and self-suspension.

**Alternatives Considered**:
- *API Middleware / ASP.NET Authorization Policies only*: Rejected because relying solely on API middleware leaves the application layer vulnerable to internal invocation and violates defence-in-depth principles.

## R3: Idempotent Account Suspension & Token Revocation

**Decision**: `Customer.Suspend(reason, suspendedBy)` updates aggregate state to `CustomerStatus.Suspended`, sets `SuspendedAtUtc` and `SuspensionReason`, and raises `CustomerSuspendedEvent`. Suspending an already suspended account is idempotent (returns success without error or state corruption). The command handler explicitly revokes all stored refresh tokens for that `CustomerId` in `VendorDbContext`.

**Rationale**:
- Idempotency ensures admin scripts or UI retry loops can execute safely without throwing unexpected errors.
- Active refresh token revocation guarantees that suspended users cannot mint new JWT access tokens after suspension.

## R4: Login and Checkout Interception

**Decision**: Check `customer.Status == CustomerStatus.Suspended` in `LoginCommandHandler` and `PlaceOrderCommandHandler`. If suspended, return `Result.Failure(Error.Forbidden("ACCOUNT_SUSPENDED", "Customer account is suspended."))`.

**Rationale**:
- Blocking at login prevents authentication, while blocking at `PlaceOrder` prevents checkout even if a suspended customer holds an existing unexpired access token.

## R5: Admin Customer Management Surface & Rate-Limiting Policy Reuse

**Decision**: Map new admin endpoints under `/api/v1/admin/customers` in `src/Vendor.Api/Endpoints/AdminCustomerEndpoints.cs`. Reuse the existing `"auth"` rate-limiting policy (`.RequireRateLimiting("auth")`) for `promote` and `demote` endpoints.

**Rationale**:
- Reuses the existing 4-policy rate limiter registered in `ServiceExtensions.cs` (`"auth"` policy allows 10 req/min with queue limit 0).
- Provides paginated, filterable listing (`email`, `role`, `status`, date range), profile + order history detail, suspend/reactivate, promote/demote (SuperAdmin), and audit-log (SuperAdmin).
