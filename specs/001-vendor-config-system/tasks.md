# Tasks: Vendor Configuration System

**Input**: Design documents from `specs/001-vendor-config-system/` (`plan.md`, `spec.md`, `data-model.md`, `contracts/`, `research.md`, `quickstart.md`)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to user stories from spec.md ([US1], [US2], [US3])
- File paths are exact and project-relative or absolute where specified.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initial project files and base configuration templates.

- [x] T001 Initialize vendor configuration file template in `config/vendor.config.json`
- [x] T002 Create secret fields manifest in `scripts/secret-fields.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core Domain models, enums, value objects, and interfaces required by all user stories.

- [x] T003 [P] Create Domain enums (`CacheProvider`, `EmailProvider`, `TaxStrategy`, `TextDirection`, `CaptureMode`, `SecretBackend`) in `src/Vendor.Domain/Enums/`
- [x] T004 [P] Create `SecretReference` value object in `src/Vendor.Domain/ValueObjects/SecretReference.cs`
- [x] T005 [P] Create configuration value objects (`BrandingConfig`, `LocaleConfig`, `TaxConfig`, `CheckoutConfig`, `AuthConfig`, `CachingConfig`, `EmailConfig`, `AnalyticsConfig`, `PromotionsConfig`, `FeatureFlags`) in `src/Vendor.Domain/ValueObjects/`
- [x] T006 [P] Create provider value objects (`PaymentProviderConfig`, `PaymentCredentials`, `ShippingProviderConfig`) in `src/Vendor.Domain/ValueObjects/`
- [x] T007 [P] Create tier value objects (`VendorBuildConfig`, `VendorBootConfig`, `VendorRuntimeConfig`) in `src/Vendor.Domain/Aggregates/VendorSettings/`
- [x] T008 Create `VendorConfig` aggregate root in `src/Vendor.Domain/Aggregates/VendorSettings/VendorConfig.cs`
- [x] T009 [P] Create Domain interfaces (`ISecretResolver`, `IVendorSettingsRepository`) in `src/Vendor.Domain/Interfaces/`
- [x] T010 [P] Create `VendorSettingsUpdatedEvent` domain event in `src/Vendor.Domain/Events/VendorSettingsUpdatedEvent.cs`

---

## Phase 3: User Story 1 - Three-Tier Configuration Resolution & Boot Validation (Priority: P1) 🎯 MVP

**Goal**: Load and resolve vendor configuration across three strict tiers (build-time, boot-time, runtime) upon container startup and fail fast if invalid or unresolvable secrets exist.

**Independent Test**: Start application container with valid vs invalid `vendor.config.json` files and secret reference sources (`ref:env:*`), verifying valid setups start successfully while invalid setups halt with non-zero exit codes.

### Tests for User Story 1

- [x] T011 [P] [US1] Unit test `SecretReference` pattern validation and masking in `tests/Vendor.Domain.Tests/ValueObjects/SecretReferenceTests.cs`
- [x] T012 [P] [US1] Unit test `VendorConfigValidator` rules (default payment provider, currency/language inclusion) in `tests/Vendor.Application.Tests/Validators/VendorConfigValidatorTests.cs`
- [x] T013 [P] [US1] Unit test `SecretResolutionFilter` startup fail-fast behavior in `tests/Vendor.Infrastructure.Tests/Config/SecretResolutionFilterTests.cs`
- [x] T014 [P] [US1] Component test `VendorConfigValidationFilter` startup halt behavior in `tests/Vendor.Infrastructure.Tests/Config/VendorConfigValidationFilterTests.cs`

### Implementation for User Story 1

- [x] T015 [P] [US1] Implement secret resolvers (`EnvironmentSecretResolver`, `VaultSecretResolver`, `AwsSsmSecretResolver`, `CompositeSecretResolver`) in `src/Vendor.Infrastructure/Config/`
- [x] T016 [P] [US1] Implement `ResolvedSecretStore` in-memory secret cache in `src/Vendor.Infrastructure/Config/ResolvedSecretStore.cs`
- [x] T017 [US1] Implement `VendorConfigValidator` FluentValidation rules in `src/Vendor.Application/Validators/VendorConfigValidator.cs`
- [x] T018 [US1] Implement `SecretResolutionFilter` `IStartupFilter` in `src/Vendor.Infrastructure/Config/SecretResolutionFilter.cs`
- [x] T019 [US1] Implement `VendorConfigValidationFilter` `IStartupFilter` in `src/Vendor.Infrastructure/Config/VendorConfigValidationFilter.cs`
- [x] T020 [US1] Register boot validation services and startup filters in `src/Vendor.Infrastructure/DependencyInjection.cs`

**Checkpoint**: User Story 1 MVP is fully functional. Application loads configuration, resolves secrets, and fails fast at boot on invalid settings.

---

## Phase 4: User Story 2 - Admin API for Dynamic Runtime Configuration Management (Priority: P2)

**Goal**: View and update runtime-tier configuration settings (branding, checkout rules, feature flags, shipping/payment provider parameters) via an Admin API without container restart.

**Independent Test**: Execute `GET /api/v1/admin/config` (masked secrets) and `PATCH /api/v1/admin/config` requests, verifying runtime updates persist and take effect immediately while build/boot tiers reject modifications.

### Tests for User Story 2

- [x] T021 [P] [US2] Unit test `GetVendorConfigQuery` handler in `tests/Vendor.Application.Tests/Handlers/GetVendorConfigHandlerTests.cs`
- [x] T022 [P] [US2] Unit test `UpdateVendorSettingsCommand` handler and versioning in `tests/Vendor.Application.Tests/Handlers/UpdateVendorSettingsHandlerTests.cs`
- [x] T023 [P] [US2] Integration test `VendorSettingsRepository` EF Core persistence in `tests/Vendor.Infrastructure.Tests/Persistence/VendorSettingsRepositoryTests.cs`
- [x] T024 [P] [US2] End-to-End API test `VendorSettingsEndpoints` in `tests/Vendor.Api.Tests/Endpoints/VendorSettingsEndpointTests.cs`

### Implementation for User Story 2

- [x] T025 [P] [US2] Implement EF Core entity configuration `VendorSettingsConfiguration` in `src/Vendor.Infrastructure/Persistence/Configurations/VendorSettingsConfiguration.cs`
- [x] T026 [US2] Implement `VendorSettingsRepository` in `src/Vendor.Infrastructure/Persistence/Repositories/VendorSettingsRepository.cs`
- [x] T027 [P] [US2] Create DTOs (`VendorConfigDto`, `VendorConfigPatchDto`) in `src/Vendor.Application/DTOs/`
- [x] T028 [US2] Implement `VendorConfigPatchValidator` (immutability guard for build/boot tiers) in `src/Vendor.Application/Validators/VendorConfigPatchValidator.cs`
- [x] T029 [US2] Implement `GetVendorConfigQuery` and handler in `src/Vendor.Application/Queries/VendorSettings/`
- [x] T030 [US2] Implement `UpdateVendorSettingsCommand` and handler (with outbox event dispatch) in `src/Vendor.Application/Commands/VendorSettings/`
- [x] T031 [US2] Implement Admin API Minimal API endpoints (`GET /api/v1/admin/config`, `PATCH /api/v1/admin/config`, `GET /api/v1/admin/config/schema`) in `src/Vendor.Api/Endpoints/VendorSettingsEndpoints.cs`

**Checkpoint**: User Stories 1 AND 2 are complete. Dynamic runtime configuration updates are live via Admin API with zero restart needed.

---

## Phase 5: User Story 3 - CI/CD Schema & Secret-Reference Audit Pipeline (Priority: P3)

**Goal**: Automatically validate JSON Schema and perform secret-reference audits against `vendor.config.json` in CI prior to deployment.

**Independent Test**: Execute `ajv-cli` schema validation and `node scripts/audit-secrets.js` against valid, schema-invalid, and raw-secret-containing config files to verify exit codes.

### Implementation for User Story 3

- [x] T032 [P] [US3] Create JSON Schema definition (Draft 2020-12) in `config/vendor.config.schema.json`
- [x] T033 [P] [US3] Create secret audit Node.js script in `scripts/audit-secrets.js`
- [x] T034 [US3] Create GitHub Actions CI workflow in `.github/workflows/validate-vendor-config.yml`

**Checkpoint**: CI validation pipeline is complete. Committed raw secrets and invalid schemas automatically break CI builds.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final wiring, Program.cs integration, and verification against quickstart scenarios.

- [x] T035 Wire all endpoints and filters in `src/Vendor.Api/Program.cs`
- [x] T036 Execute validation scenarios from `specs/001-vendor-config-system/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Can start immediately.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational phase completion.
- **User Story 2 (Phase 4)**: Depends on Foundational phase completion + US1 models.
- **User Story 3 (Phase 5)**: Depends on Phase 1 setup files (`vendor.config.json`, `secret-fields.json`).
- **Polish (Phase 6)**: Depends on all user stories being complete.

---

## Parallel Opportunities

- **Phase 2 Foundational**: T003, T004, T005, T006, T007, T009, T010 can all run concurrently.
- **Phase 3 US1**: T011, T012, T013, T014 (Tests) and T015, T016 (Resolvers/Stores) can run in parallel.
- **Phase 4 US2**: T021, T022, T023, T024 (Tests) and T025, T027 can run in parallel.
- **Phase 5 US3**: T032, T033 can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Verify startup resolution & fail-fast validation end-to-end.

### Incremental Delivery

1. Foundation ready (Phase 1 + 2)
2. Add US1 → Boot resolution & validation (MVP!)
3. Add US2 → Dynamic Admin API updates
4. Add US3 → CI/CD schema & secret audit pipeline
5. Polish & quickstart validation (Phase 6)
