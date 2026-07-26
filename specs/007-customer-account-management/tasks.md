# Tasks: Customer Account Management

**Input**: Design documents from `/specs/007-customer-account-management/`

**Prerequisites**: [plan.md](./plan.md) · [spec.md](./spec.md) · [research.md](./research.md) · [data-model.md](./data-model.md) · [contracts/admin-customer-api.md](./contracts/admin-customer-api.md) · [quickstart.md](./quickstart.md)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in every task description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify project solution build and project references before starting additive extensions.

- [x] T001 Verify `dotnet build Vendor.slnx` completes with zero errors across all projects

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain aggregate extensions and outbox events that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Create `CustomerRole` and `CustomerStatus` enums in `src/Vendor.Domain/Aggregates/Customer/CustomerRole.cs` and `src/Vendor.Domain/Aggregates/Customer/CustomerStatus.cs`
- [x] T003 [P] Create `CustomerSuspendedEvent`, `CustomerReactivatedEvent`, and `CustomerRoleChangedEvent` domain events in `src/Vendor.Domain/Events/CustomerEvents.cs`
- [x] T004 Extend `Customer` aggregate in `src/Vendor.Domain/Aggregates/Customer/Customer.cs` with `Role`, `Status`, `SuspendedAtUtc`, `SuspensionReason`, `RoleChangedAtUtc`, `RoleChangedByCustomerId`, domain methods (`Suspend`, `Reactivate`, `ChangeRole`), and domain event dispatches
- [x] T005 [P] Create `CustomerAuditLog` entity in `src/Vendor.Domain/Aggregates/Customer/CustomerAuditLog.cs`
- [x] T006 Extend `ICustomerRepository` interface in `src/Vendor.Domain/Interfaces/Repositories/ICustomerRepository.cs` with `GetPagedAsync` and `GetAuditLogsAsync`

**Checkpoint**: Foundation ready — domain aggregate extended and core interfaces ready.

---

## Phase 3: User Story 1 — Customer Account Suspension & Access Control Enforcement (Priority: P1) 🎯 MVP

**Goal**: Enable account suspension/reactivation, immediate refresh token revocation, and block suspended accounts at login and checkout with `ACCOUNT_SUSPENDED`.

**Independent Test**: Suspend an active customer account, verify refresh tokens are revoked, and confirm that both login and checkout attempts for that customer are rejected with an `ACCOUNT_SUSPENDED` error.

- [x] T007 [P] [US1] Create `SuspendCustomerCommand` and handler in `src/Vendor.Application/Modules/Customers/Commands/SuspendCustomerCommand.cs` and `src/Vendor.Application/Modules/Customers/CustomerHandlers.cs` (idempotent suspension, outbox event, refresh token revocation)
- [x] T008 [P] [US1] Create `ReactivateCustomerCommand` and handler in `src/Vendor.Application/Modules/Customers/Commands/ReactivateCustomerCommand.cs` and `src/Vendor.Application/Modules/Customers/CustomerHandlers.cs`
- [x] T009 [US1] Update `LoginCommandHandler` in `src/Vendor.Application/Modules/Auth/AuthHandlers.cs` to check `customer.Status == CustomerStatus.Suspended` and return `Result.Failure(Error.Forbidden("ACCOUNT_SUSPENDED", "Customer account is suspended."))`
- [x] T010 [US1] Update `PlaceOrderCommandHandler` in `src/Vendor.Application/Modules/Orders/OrderHandlers.cs` to check `customer.Status == CustomerStatus.Suspended` and return `Result.Failure(Error.Forbidden("ACCOUNT_SUSPENDED", "Customer account is suspended."))`
- [x] T011 [US1] Update EF Core entity mapping in `src/Vendor.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` and `CustomerAuditLogConfiguration.cs`, then generate database migration `dotnet ef migrations add CustomerAccountManagement`
- [x] T012 [P] [US1] Update `CustomerRepository` in `src/Vendor.Infrastructure/Persistence/Repositories/CustomerRepository.cs` to implement suspension state persistence and refresh token revocation execution in `VendorDbContext`
- [x] T013 [P] [US1] Map `POST /api/v1/admin/customers/{id}/suspend` and `POST /api/v1/admin/customers/{id}/reactivate` endpoints in `src/Vendor.Api/Endpoints/AdminCustomerEndpoints.cs`
- [x] T014 [P] [US1] Add domain unit tests and handler integration tests for suspension, reactivation, token revocation, and login/checkout guards in `tests/Vendor.Domain.Tests/Aggregates/CustomerTests.cs` and `tests/Vendor.Application.Tests/Handlers/CustomerHandlerTests.cs`

**Checkpoint**: User Story 1 complete — suspended accounts are blocked from login/checkout and refresh tokens are revoked.

---

## Phase 4: User Story 2 — Role Promotion & Demotion with SuperAdmin Guards (Priority: P2)

**Goal**: Enable role promotion and demotion restricted strictly to SuperAdmin callers, with handler-level enforcement, self-modification prevention, and blocking of SuperAdmin role assignment.

**Independent Test**: Verify that non-SuperAdmin callers cannot promote/demote users (validated inside command handler), SuperAdmin self-demotion is rejected, and `SuperAdmin` role assignment via API is blocked.

- [x] T015 [P] [US2] Create `PromoteCustomerCommand` and handler in `src/Vendor.Application/Modules/Customers/Commands/PromoteCustomerCommand.cs` and `src/Vendor.Application/Modules/Customers/CustomerHandlers.cs` (verifying caller is SuperAdmin, preventing SuperAdmin role assignment)
- [x] T016 [P] [US2] Create `DemoteCustomerCommand` and handler in `src/Vendor.Application/Modules/Customers/Commands/DemoteCustomerCommand.cs` and `src/Vendor.Application/Modules/Customers/CustomerHandlers.cs` (verifying caller is SuperAdmin, preventing SuperAdmin self-demotion)
- [x] T017 [P] [US2] Map `POST /api/v1/admin/customers/{id}/promote` and `POST /api/v1/admin/customers/{id}/demote` in `src/Vendor.Api/Endpoints/AdminCustomerEndpoints.cs` configured with `.RequireRateLimiting("auth")` and `SuperAdmin` authorization
- [x] T018 [P] [US2] Add unit and handler tests for SuperAdmin authority validation, self-demotion rejection, and unassignable SuperAdmin role rules in `tests/Vendor.Domain.Tests/Aggregates/CustomerTests.cs` and `tests/Vendor.Application.Tests/Handlers/CustomerHandlerTests.cs`

**Checkpoint**: User Story 2 complete — role promotion/demotion operates safely under strict SuperAdmin handler guards.

---

## Phase 5: User Story 3 — Admin Customer Management Surface & Audit Trail (Priority: P3)

**Goal**: Provide admin paginated customer listing with filters, customer detail with order history, SuperAdmin audit log query endpoint, and rate-limiting enforcement.

**Independent Test**: Execute paginated customer search with role/status filters, view customer profile with order history, and query audit log for suspension and role change records.

- [x] T019 [P] [US3] Create `GetAdminCustomersQuery` and handler in `src/Vendor.Application/Modules/Customers/Queries/GetAdminCustomersQuery.cs` and `src/Vendor.Application/Modules/Customers/CustomerHandlers.cs` (filtering by email, role, status, registration date range)
- [x] T020 [P] [US3] Create `GetCustomerDetailQuery` and handler in `src/Vendor.Application/Modules/Customers/Queries/GetCustomerDetailQuery.cs` and `src/Vendor.Application/Modules/Customers/CustomerHandlers.cs` (returning profile + order history)
- [x] T021 [P] [US3] Create `GetCustomerAuditLogsQuery` and handler in `src/Vendor.Application/Modules/Customers/Queries/GetCustomerAuditLogsQuery.cs` and `src/Vendor.Application/Modules/Customers/CustomerHandlers.cs` (SuperAdmin only)
- [x] T022 [US3] Implement `GetPagedAsync` and `GetAuditLogsAsync` in `src/Vendor.Infrastructure/Persistence/Repositories/CustomerRepository.cs`
- [x] T023 [P] [US3] Map `GET /api/v1/admin/customers`, `GET /api/v1/admin/customers/{id}`, and `GET /api/v1/admin/customers/{id}/audit-log` in `src/Vendor.Api/Endpoints/AdminCustomerEndpoints.cs`
- [x] T024 [P] [US3] Add API integration tests in `tests/Vendor.Api.Tests/Integration/AdminCustomerEndpointsTests.cs` covering filtering, profile + order history, audit log output, and rate limiting throttling
- [x] T025 [P] Run full test suite `dotnet test Vendor.slnx` and verify coverage thresholds (Domain ≥90%, Application ≥85%, Infrastructure ≥70%, API ≥75%)
- [x] T026 [P] Execute and validate all scenarios in `quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Phase 2 completion
  - US1 (P1) → US2 (P2) → US3 (P3)
- **Polish (Phase 6)**: Depends on all user stories being complete

### Parallel Opportunities

- **Phase 2**: T002, T003, T005 can run in parallel
- **Phase 3 (US1)**: T007, T008, T012, T013, T014 can run in parallel
- **Phase 4 (US2)**: T015, T016, T017, T018 can run in parallel
- **Phase 5 (US3)**: T019, T020, T021, T023, T024 can run in parallel
