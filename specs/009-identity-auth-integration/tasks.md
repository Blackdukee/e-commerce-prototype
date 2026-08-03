# Tasks: Identity Auth Integration

**Input**: Design documents from `/specs/009-identity-auth-integration/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/auth-endpoints.md, quickstart.md

**Tests**: Unit and integration test tasks are included to satisfy Constitution Rule VII coverage targets.

**Organization**: Tasks are grouped by user story (US1, US2, US3) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify ASP.NET Core Identity dependencies and OAuth configuration classes

- [x] T001 Verify ASP.NET Core Identity NuGet package references in `src/Vendor.Infrastructure/Vendor.Infrastructure.csproj`
- [x] T002 [P] Create Google and Facebook OAuth configuration options in `src/Vendor.Infrastructure/Identity/OAuthOptions.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Identity user entity, EF Core mapping, and service registration that MUST be complete before user story handlers can be built

**⚠️ CRITICAL**: No user story command handler work can begin until this phase is complete

- [x] T003 Create ApplicationUser identity entity inheriting from IdentityUser<Guid> with CustomerId property in `src/Vendor.Infrastructure/Identity/ApplicationUser.cs`
- [x] T004 [P] Create EF Core entity configuration ApplicationUserConfiguration mapping CustomerId unique 1:1 foreign key in `src/Vendor.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`
- [x] T005 Update VendorDbContext in `src/Vendor.Infrastructure/Persistence/VendorDbContext.cs` to register ApplicationUser and Identity tables
- [x] T006 Register Identity services (AddIdentityCore<ApplicationUser>, password hasher, 5-failed-attempt / 15-min lockout policy) in `src/Vendor.Infrastructure/DependencyInjection.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Secure Identity Password Authentication & Registration (Priority: P1) 🎯 MVP

**Goal**: Enable users to register and sign in with password credentials via ASP.NET Core Identity, creating ApplicationUser and Customer aggregate atomically in a single transaction while enforcing a 5-failed-attempt 15-minute lockout policy.

**Independent Test**: Register a new user via POST /api/v1/auth/register, verify ApplicationUser and Customer aggregate share matching CustomerId, and verify POST /api/v1/auth/login issues JWT token pairs and locks out after 5 invalid attempts.

### Tests for User Story 1

- [x] T007 [P] [US1] Unit test ApplicationUser entity initialization and CustomerId FK property in `tests/Vendor.Infrastructure.Tests/Identity/ApplicationUserTests.cs`
- [x] T008 [P] [US1] Unit test RegisterCommandHandler atomic transaction handling in `tests/Vendor.Application.Tests/Auth/RegisterCommandHandlerTests.cs`
- [x] T009 [P] [US1] Unit test LoginCommandHandler with CheckPasswordSignInAsync and lockout checking in `tests/Vendor.Application.Tests/Auth/LoginCommandHandlerTests.cs`

### Implementation for User Story 1

- [x] T010 [US1] Implement atomic registration transaction creating Customer aggregate and ApplicationUser together in `src/Vendor.Application/Modules/Auth/AuthHandlers.cs`
- [x] T011 [US1] Update LoginCommandHandler to execute CheckPasswordSignInAsync with lockoutOnFailure: true and issue JWT token pair with email_verified claim in `src/Vendor.Application/Modules/Auth/AuthHandlers.cs`
- [x] T012 [US1] Integration test registration and password sign-in with lockout enforcement in `tests/Vendor.Api.Tests/Auth/IdentityAuthEndpointsTests.cs`

**Checkpoint**: User Story 1 (MVP) fully functional and independently testable

---

## Phase 4: User Story 2 - Google & Facebook External Provider OAuth Integration (Priority: P2)

**Goal**: Validate Google ID tokens and Facebook Graph API tokens server-side, linking external provider keys to existing accounts (if email is verified) or creating paired ApplicationUser + Customer aggregates atomically.

**Independent Test**: Post a valid Google ID token to POST /api/v1/auth/external/google. Verify that first-time login creates both identity and Customer aggregate atomically, while unverified email attempts for existing accounts fail with a 409 Conflict.

### Tests for User Story 2

- [x] T013 [P] [US2] Unit test GoogleExternalAuthService ID token public key validation in `tests/Vendor.Infrastructure.Tests/Identity/GoogleExternalAuthServiceTests.cs`
- [x] T014 [P] [US2] Unit test FacebookExternalAuthService Graph API /me token verification in `tests/Vendor.Infrastructure.Tests/Identity/FacebookExternalAuthServiceTests.cs`
- [x] T015 [P] [US2] Unit test ExternalLoginCommandHandler verified email account takeover conflict handling in `tests/Vendor.Application.Tests/Auth/ExternalLoginCommandHandlerTests.cs`

### Implementation for User Story 2

- [x] T016 [US2] Implement IGoogleExternalAuthService and GoogleExternalAuthService using GoogleJsonWebSignature in `src/Vendor.Infrastructure/Identity/GoogleExternalAuthService.cs`
- [x] T017 [US2] Implement IFacebookExternalAuthService and FacebookExternalAuthService using Graph API /me in `src/Vendor.Infrastructure/Identity/FacebookExternalAuthService.cs`
- [x] T018 [US2] Implement ExternalLoginCommandHandler handling FindByLoginAsync, FindByEmailAsync, unverified email 409 conflict checks, and atomic account creation in `src/Vendor.Application/Modules/Auth/AuthHandlers.cs`
- [x] T019 [US2] Map POST /api/v1/auth/external/google and POST /api/v1/auth/external/facebook in `src/Vendor.Api/Endpoints/AuthEndpoints.cs`
- [x] T020 [US2] Integration test Google and Facebook external OAuth login flows and 409 unverified email conflict in `tests/Vendor.Api.Tests/Auth/ExternalOAuthEndpointsTests.cs`

**Checkpoint**: User Stories 1 AND 2 fully functional and independently testable

---

## Phase 5: User Story 3 - Identity Lifecycle Email Verification & Password Reset (Priority: P3)

**Goal**: Power email verification and password reset workflows using ASP.NET Core Identity token generation and confirmation services.

**Independent Test**: Request password reset via POST /api/v1/auth/forgot-password, obtain identity reset token, and reset password via POST /api/v1/auth/reset-password, confirming the new password allows login.

### Tests for User Story 3

- [x] T021 [P] [US3] Unit test VerifyEmailCommandHandler using ConfirmEmailAsync in `tests/Vendor.Application.Tests/Auth/VerifyEmailCommandHandlerTests.cs`
- [x] T022 [P] [US3] Unit test ForgotPasswordCommandHandler and ResetPasswordCommandHandler in `tests/Vendor.Application.Tests/Auth/ResetPasswordCommandHandlerTests.cs`

### Implementation for User Story 3

- [x] T023 [US3] Wire VerifyEmailCommandHandler to GenerateEmailConfirmationTokenAsync / ConfirmEmailAsync in `src/Vendor.Application/Modules/Auth/AuthHandlers.cs`
- [x] T024 [US3] Wire ForgotPasswordCommandHandler and ResetPasswordCommandHandler to GeneratePasswordResetTokenAsync / ResetPasswordAsync in `src/Vendor.Application/Modules/Auth/AuthHandlers.cs`
- [x] T025 [US3] Integration test email verification and password reset lifecycle in `tests/Vendor.Api.Tests/Auth/IdentityLifecycleEndpointsTests.cs`

**Checkpoint**: All user stories fully functional and independently testable

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Database migrations, swagger documentation, and end-to-end quickstart validation

- [x] T026 [P] Add EF Core migration AddIdentityAuthIntegration for AspNetUsers CustomerId foreign key and external login tables in `src/Vendor.Infrastructure/Migrations/`
- [x] T027 Update OpenAPI swagger documentation for auth endpoints in `src/Vendor.Api/Endpoints/AuthEndpoints.cs`
- [x] T028 Execute quickstart.md validation scenarios and verify layer test coverage targets across Domain, Application, Infrastructure, and API projects

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User Story 1 (P1 - MVP) -> User Story 2 (P2) -> User Story 3 (P3)
- **Polish (Phase 6)**: Depends on all user stories being complete
