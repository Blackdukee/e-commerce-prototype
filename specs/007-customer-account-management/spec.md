# Feature Specification: Customer Account Management

**Feature Branch**: `007-customer-account-management`

**Created**: 2026-07-26

**Status**: Draft

**Input**: User description: "Extend the Customer aggregate with account-management capability. Add a Role field (Customer, Admin, SuperAdmin) and a Status field (Active, Suspended), plus suspension metadata (timestamp, reason) and role-change audit metadata (timestamp, who changed it) directly on the aggregate. Suspending an account is idempotent, immediately revokes all of that customer's refresh tokens, and blocks future login and checkout attempts with an ACCOUNT_SUSPENDED error until reactivated. Promoting a customer to Admin or demoting an Admin back to Customer can only be performed by a caller whose own role is SuperAdmin — this check lives in the command handler itself, not only in API-layer authorization, so it can't be bypassed. SuperAdmin is never assignable through any command or endpoint — it exists only as a seed value configured at deploy time — and a SuperAdmin can never demote or suspend their own account. Add three domain events: CustomerSuspendedEvent, CustomerReactivatedEvent, and CustomerRoleChangedEvent (the last one recording the previous role, new role, and which customer performed the change), all delivered through the existing transactional outbox. Add an admin-facing customer management surface: a paginated, filterable list endpoint (filter by email, role, status, registration date range), a get-by-id endpoint returning profile plus order history, a suspend/reactivate endpoint, promote/demote endpoints restricted to SuperAdmin, and an audit-log endpoint (also SuperAdmin-only) showing the suspension and role-change history for a single account. Promote and demote endpoints should sit under the same tight rate-limit policy as login/register rather than the general authenticated-endpoint policy."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Customer Account Suspension & Access Control Enforcement (Priority: P1)

As an Admin or SuperAdmin, I want to suspend a customer account that violates terms or shows suspicious activity, so that they are immediately blocked from logging in or placing orders.

**Why this priority**: Account suspension is a critical security and fraud-mitigation capability required to safeguard the store. Revoking active tokens and blocking checkout prevents unauthorized or fraudulent operations.

**Independent Test**: Can be tested independently by creating an active customer, calling the suspend endpoint as an Admin/SuperAdmin, verifying that refresh tokens are revoked, and attempting login or checkout to confirm the request is rejected with `ACCOUNT_SUSPENDED`.

**Acceptance Scenarios**:

1. **Given** an active customer account, **When** an authorized admin suspends the account with a specified reason, **Then** the customer status changes to `Suspended`, suspension metadata (timestamp, reason) is recorded directly on the aggregate, all active refresh tokens for the customer are revoked, and a `CustomerSuspendedEvent` is published to the outbox.
2. **Given** an already suspended customer account, **When** an admin calls the suspend action again (idempotent call), **Then** the request succeeds without error and without duplicating revocation logic or throwing errors.
3. **Given** a suspended customer account, **When** the customer attempts to log in or proceed through checkout, **Then** the request is blocked and returns an `ACCOUNT_SUSPENDED` error response.
4. **Given** a suspended customer account, **When** an authorized admin reactivates the account, **Then** the customer status changes back to `Active`, suspension metadata is cleared/updated, a `CustomerReactivatedEvent` is published, and the customer can log in and check out again.

---

### User Story 2 - Role Promotion & Demotion with SuperAdmin Guards (Priority: P2)

As a SuperAdmin, I want to promote regular Customers to Admins and demote Admins back to Customers, with strict security checks preventing self-demotion and unauthorized role assignments.

**Why this priority**: Administrative privilege management must be strictly guarded to prevent privilege escalation. Command-handler-level enforcement ensures rules cannot be bypassed even if API authorization fails or is bypassed internally.

**Independent Test**: Can be tested independently by attempting to promote/demote users using callers with different roles (`Customer`, `Admin`, `SuperAdmin`), verifying handler-level rejection for non-SuperAdmin callers, and testing edge cases like SuperAdmin self-demotion or attempting to assign `SuperAdmin`.

**Acceptance Scenarios**:

1. **Given** a customer account with role `Customer`, **When** a `SuperAdmin` caller issues a promote command, **Then** the customer role changes to `Admin`, role-change metadata (timestamp, who changed it) is updated on the aggregate, and a `CustomerRoleChangedEvent` is published.
2. **Given** a customer account with role `Admin`, **When** a non-SuperAdmin caller (such as another `Admin` or `Customer`) attempts to promote or demote the user, **Then** the command handler rejects the command with an unauthorized error regardless of API layer routing.
3. **Given** a caller whose role is `SuperAdmin`, **When** the SuperAdmin attempts to demote or suspend their own account, **Then** the domain logic rejects the operation with a self-modification rule violation error.
4. **Given** any admin endpoint or command, **When** any caller attempts to assign the `SuperAdmin` role to any customer account, **Then** the operation is rejected because `SuperAdmin` is unassignable through endpoints/commands.

---

### User Story 3 - Admin Customer Management Surface & Audit Trail (Priority: P3)

As an Admin or SuperAdmin, I want to list, filter, inspect profiles with order histories, and audit account security changes, so that I can efficiently manage the customer base and maintain accountability.

**Why this priority**: Management UIs and audit logs provide essential visibility and operational tools for customer support and governance.

**Independent Test**: Can be tested independently by querying the paginated list endpoint with various filter parameters, fetching a customer detail view containing order history, and querying the SuperAdmin-only audit-log endpoint for account history.

**Acceptance Scenarios**:

1. **Given** multiple customer accounts, **When** an admin queries the list endpoint with filters (email search, role, status, registration date range) and pagination parameters, **Then** the system returns a paginated list matching the exact filter criteria.
2. **Given** a specific customer ID, **When** an admin requests the customer profile, **Then** the response includes the full customer profile metadata and their complete order history.
3. **Given** a specific customer account, **When** a `SuperAdmin` queries the audit-log endpoint for that account, **Then** the system returns the chronological history of all suspension and role-change events for that account.
4. **Given** the promote and demote endpoints, **When** requests arrive in rapid succession, **Then** they are throttled under the tight auth rate-limit policy (same as login/register) rather than the general API rate-limit policy.

---

### Edge Cases

- What happens when a suspended customer attempts to use an existing unexpired JWT access token? The access token check verifies customer status or token revocation state, blocking the request with `ACCOUNT_SUSPENDED`.
- What happens if a SuperAdmin account is configured at boot time? The SuperAdmin role exists as a seed value defined at deployment/boot time, never created via runtime endpoints.
- How does the system handle rapid repeated promote/demote calls? Tight rate limiting blocks excessive requests, while domain events record every legitimate role transition accurately.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST extend the `Customer` aggregate with a `Role` field supporting values `Customer`, `Admin`, and `SuperAdmin`.
- **FR-002**: System MUST extend the `Customer` aggregate with a `Status` field supporting values `Active` and `Suspended`.
- **FR-003**: System MUST record suspension metadata (`SuspendedAtUtc`, `SuspensionReason`) and role-change metadata (`RoleChangedAtUtc`, `RoleChangedByCustomerId`) directly on the `Customer` aggregate.
- **FR-004**: Suspending a customer account MUST be idempotent, succeeding without error if the account is already suspended.
- **FR-005**: Suspending an account MUST immediately revoke all refresh tokens associated with that customer.
- **FR-006**: System MUST block suspended accounts from completing login or checkout operations, returning an explicit `ACCOUNT_SUSPENDED` error.
- **FR-007**: Promoting a customer to `Admin` or demoting an `Admin` to `Customer` MUST be restricted exclusively to callers whose own role is `SuperAdmin`.
- **FR-008**: The `SuperAdmin` role check MUST be enforced within the Application command handler itself to ensure security rules cannot be bypassed.
- **FR-009**: System MUST prevent assigning the `SuperAdmin` role through any command, API endpoint, or runtime operation.
- **FR-010**: System MUST prevent a `SuperAdmin` from demoting or suspending their own account.
- **FR-011**: System MUST publish `CustomerSuspendedEvent`, `CustomerReactivatedEvent`, and `CustomerRoleChangedEvent` to the transactional outbox upon relevant aggregate state changes. `CustomerRoleChangedEvent` MUST include previous role, new role, and the ID of the customer who performed the change.
- **FR-012**: System MUST provide an admin-facing paginated list endpoint allowing filtering by email, role, status, and registration date range.
- **FR-013**: System MUST provide an admin-facing get-by-ID endpoint returning the customer profile along with their order history.
- **FR-014**: System MUST provide admin-facing endpoints for suspending and reactivating customer accounts.
- **FR-015**: System MUST provide SuperAdmin-only endpoints for promoting and demoting customer roles.
- **FR-016**: System MUST provide a SuperAdmin-only audit-log endpoint returning the full suspension and role-change history for a specified customer account.
- **FR-017**: The promote and demote endpoints MUST be governed by the tight authentication rate-limiting policy (same as login/register) rather than general API rate limits.

### Key Entities *(include if feature involves data)*

- **Customer (Aggregate Root)**:
  - `Id`: CustomerId
  - `Email`: String
  - `FirstName`: String
  - `LastName`: String
  - `CustomerType`: Guest | Registered
  - `Role`: Customer | Admin | SuperAdmin
  - `Status`: Active | Suspended
  - `SuspendedAtUtc`: DateTime?
  - `SuspensionReason`: String?
  - `RoleChangedAtUtc`: DateTime?
  - `RoleChangedByCustomerId`: CustomerId?
  - `CreatedAtUtc`: DateTime
- **AuditTrail / CustomerHistory**:
  - `Id`: Guid
  - `CustomerId`: CustomerId
  - `EventType`: RoleChanged | Suspended | Reactivated
  - `Details`: JSON or structured metadata (previous role, new role, reason, actor ID)
  - `TimestampUtc`: DateTime

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Account suspension revokes refresh tokens and blocks checkout/login in under 1 second.
- **SC-002**: 100% of promote and demote requests are validated in the command handler against SuperAdmin authority before state mutation occurs.
- **SC-003**: 0% of runtime requests can assign the `SuperAdmin` role or perform self-demotion/suspension of a `SuperAdmin`.
- **SC-004**: Admin paginated customer list queries return in under 200ms for up to 100,000 customer records.
- **SC-005**: 100% of suspension and role-change actions emit transactional outbox events without data loss.

## Assumptions

- Newly registered accounts default to `Role = Customer` and `Status = Active`.
- The initial `SuperAdmin` account is seeded at deployment/boot time via configuration settings (`vendor.config.json`).
- Existing customer records in database migrations default to `Role = Customer` and `Status = Active`.
- Refresh token revocation invalidates stored refresh tokens in the database/token store.
