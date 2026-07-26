# Tasks: Test Suite & CI/CD Pipeline

**Input**: Design documents from `/specs/006-test-suite-cicd-pipeline/`

**Prerequisites**: [plan.md](./plan.md) · [spec.md](./spec.md) · [research.md](./research.md) · [data-model.md](./data-model.md) · [contracts/contracts.md](./contracts/contracts.md) · [quickstart.md](./quickstart.md)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths are included in every task description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Upgrade test project package references, add missing packages, and establish shared fixture/helper scaffolding that all later phases depend on.

- [X] T001 Upgrade `tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj`: remove `Microsoft.EntityFrameworkCore.InMemory` and `NSubstitute`; add `Testcontainers.MsSql` v4.3.0, `Respawn` v6.2.1, `Bogus` v3.x, `Moq` v4.x
- [X] T002 [P] Add `Bogus` v3.x package to `tests/Vendor.Domain.Tests/Vendor.Domain.Tests.csproj`
- [X] T003 [P] Add `Bogus` v3.x package to `tests/Vendor.Application.Tests/Vendor.Application.Tests.csproj`
- [X] T004 [P] Upgrade `tests/Vendor.Api.Tests/Vendor.Api.Tests.csproj`: add `Bogus` v3.x and `System.IdentityModel.Tokens.Jwt` v8.x; remove `NSubstitute`
- [X] T005 Create `tests/Vendor.Infrastructure.Tests/Fixtures/MsSqlFixture.cs` — `ICollectionFixture<MsSqlFixture>` implementing `IAsyncLifetime`: start `MsSqlContainer` (SQL Server 2022), apply EF Core migrations, create `Respawner` (ignoring `__EFMigrationsHistory`), expose `ResetAsync()`
- [X] T006 Create `tests/Vendor.Infrastructure.Tests/Fixtures/DatabaseCollectionAttribute.cs` — `[CollectionDefinition("Database")]` attribute wiring `MsSqlFixture`
- [X] T007 Create `tests/Vendor.Api.Tests/Helpers/VendorApiFactory.cs` — `WebApplicationFactory<Program>` subclass that overrides `JwtBearerOptions` via `PostConfigure<JwtBearerOptions>` with test signing key (`"vendor-test-signing-key-256-bits!!"`) and test issuer/audience
- [X] T008 Create `tests/Vendor.Api.Tests/Helpers/AuthHelper.cs` — static class with `GenerateAdminToken()`, `GenerateCustomerToken(string customerId)`, `GenerateExpiredToken()` methods using `System.IdentityModel.Tokens.Jwt`; add `HttpClient` extension methods `WithAdminBearerToken()` and `WithCustomerBearerToken()`
- [X] T009 Verify `dotnet build Vendor.slnx` completes with zero errors after all package reference changes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared Bogus `Faker<T>` generator factories used across all user story test phases. Must be complete before any test-filling work begins.

**⚠️ CRITICAL**: No user story implementation can begin until this phase is complete.

- [X] T010 [P] Create `tests/Vendor.Domain.Tests/Generators/CustomerFaker.cs` — `Faker<Customer>` using `.CustomInstantiator()` with Bogus email/phone/address; set `Randomizer.Seed = new Random(42)` in static initializer
- [X] T011 [P] Create `tests/Vendor.Domain.Tests/Generators/ProductFaker.cs` — `Faker<Product>` with realistic name, slug, `Money` price (USD), stock quantity 1–100
- [X] T012 [P] Create `tests/Vendor.Domain.Tests/Generators/OrderFaker.cs` — `Faker<Order>` with 1–5 `OrderLine` items, valid `Money` values, `Address`, calling the `Order` constructor directly
- [X] T013 [P] Create `tests/Vendor.Domain.Tests/Generators/CartFaker.cs` — `Faker<Cart>` with 1–5 cart items, valid `CustomerId` and product references
- [X] T014 [P] Create `tests/Vendor.Application.Tests/Generators/` — copy/alias `CustomerFaker`, `ProductFaker`, `OrderFaker`, `CartFaker` into Application test project's own `Generators/` folder
- [X] T015 [P] Create `tests/Vendor.Infrastructure.Tests/Generators/` — copy/alias all domain fakers; add `ShipmentFaker.cs`, `PaymentFaker.cs`, `PromotionFaker.cs` for infrastructure test data
- [X] T016 [P] Create `tests/Vendor.Api.Tests/Generators/` — add lightweight request-body fakers for HTTP integration tests (e.g., `RegisterRequestFaker`, `PlaceOrderRequestFaker`)

**Checkpoint**: Foundation ready — all generators compile; build passes; user story implementation can now proceed.

---

## Phase 3: User Story 1 — Layered Test Pyramid Suite (Priority: P1) 🎯 MVP

**Goal**: Achieve Domain ≥90%, Application ≥85%, Infrastructure ≥70%, and API ≥75% line coverage targets across all four test projects.

**Independent Test**: `dotnet test Vendor.slnx --collect:"XPlat Code Coverage"` → all tests pass, per-layer coverage meets thresholds when measured with `reportgenerator`.

### Domain Tests (Vendor.Domain.Tests)

- [X] T017 [P] [US1] Expand `tests/Vendor.Domain.Tests/Aggregates/OrderTests.cs` — add tests for every `OrderStatus` state-machine transition: all valid transitions (Pending→Confirmed, Confirmed→Processing, etc.), all invalid transitions throwing `InvalidStateTransitionException`, zero-line order construction throwing `BusinessRuleViolationException`, negative total throwing `BusinessRuleViolationException`, `RaiseDomainEvent` called on `ConfirmPayment`/`Ship`/`Deliver`/`Cancel`
- [X] T018 [P] [US1] Expand `tests/Vendor.Domain.Tests/Aggregates/CustomerAndCartTests.cs` — `Customer` registration invariants (null/empty email rejected), `Cart` add/remove/clear item behaviors, cart total calculation, cart-to-order conversion guards
- [X] T019 [P] [US1] Expand `tests/Vendor.Domain.Tests/Aggregates/ProductTests.cs` — `Product` creation invariants (empty slug, negative price), `AdjustStock` with valid/zero/negative delta, `Deactivate`/`Activate` state changes, domain event raised on creation
- [X] T020 [P] [US1] Expand `tests/Vendor.Domain.Tests/Aggregates/PaymentAndShipmentTests.cs` — `Payment` capture/fail/refund state transitions and invariants, `Shipment` creation/deliver transitions, invalid state transitions rejected
- [X] T021 [P] [US1] Expand `tests/Vendor.Domain.Tests/Aggregates/PromotionReturnAnalyticsTests.cs` — `Promotion` date range validation, discount application rules, `ReturnRequest` state machine, `AnalyticsEvent` construction invariants
- [X] T022 [P] [US1] Create `tests/Vendor.Domain.Tests/ValueObjects/MoneyTests.cs` — `Money` addition/subtraction (same currency success, different currency throws), negative amount rejected, zero amount accepted, equality semantics
- [X] T023 [P] [US1] Create `tests/Vendor.Domain.Tests/ValueObjects/AddressTests.cs` — null/empty street, city, country rejected; valid construction; equality
- [X] T024 [P] [US1] Create `tests/Vendor.Domain.Tests/ValueObjects/SlugTests.cs` — whitespace slug rejected, uppercasing/trimming applied, equality
- [X] T025 [P] [US1] Create `tests/Vendor.Domain.Tests/ValueObjects/SecretReferenceTests.cs` — valid `ref:env:`, `ref:vault:`, `ref:aws-ssm:` prefixes parsed; invalid prefix throws; raw value rejected
- [X] T026 [P] [US1] Create `tests/Vendor.Domain.Tests/ValueObjects/DateRangeTests.cs` — end before start rejected, same-day range valid, `Contains(date)` boundary logic

### Application Tests (Vendor.Application.Tests)

- [X] T027 [P] [US1] Expand `tests/Vendor.Application.Tests/Handlers/GetVendorConfigHandlerTests.cs` — mock `IVendorSettingsRepository.GetAsync()` returns settings; handler merges boot + runtime config into `VendorConfigDto`; null settings from repo returns defaults
- [X] T028 [P] [US1] Expand `tests/Vendor.Application.Tests/Handlers/UpdateVendorSettingsHandlerTests.cs` — valid command persists via mock repo, raises domain event via outbox mock, returns `Result.Success()`; invalid command (validation failure) returns `Result.Failure()` without calling repo
- [X] T029 [P] [US1] Create `tests/Vendor.Application.Tests/Handlers/PlaceOrderHandlerTests.cs` — mock `IOrderRepository`, mock `ICartRepository`; valid cart → order created, cart cleared, `OrderPlacedEvent` raised via outbox mock; empty cart returns `Result.Failure()`
- [X] T030 [P] [US1] Create `tests/Vendor.Application.Tests/Handlers/RegisterCustomerHandlerTests.cs` — mock `ICustomerRepository`; duplicate email returns `Result.Failure()`; valid registration → customer persisted and `CustomerRegisteredEvent` raised
- [X] T031 [P] [US1] Create `tests/Vendor.Application.Tests/Handlers/ProductHandlerTests.cs` — `CreateProduct`, `UpdateProduct`, `DeactivateProduct` handler tests; mock `IProductRepository`; slug uniqueness conflict returns `Result.Failure()`
- [X] T032 [P] [US1] Create `tests/Vendor.Application.Tests/Handlers/PaymentHandlerTests.cs` — `CapturePayment` handler: mock `IPaymentGatewayFactory`; success path → `Payment.Capture()` called, event raised; gateway exception wrapped into `Result.Failure()`
- [X] T033 [P] [US1] Create `tests/Vendor.Application.Tests/Modules/ValidationBehaviorTests.cs` — `ValidationBehavior<TRequest, TResponse>` pipeline: valid command passes through to `next()`; invalid command (FluentValidation errors) returns `Result.Failure()` without calling `next()`
- [X] T034 [P] [US1] Create `tests/Vendor.Application.Tests/Modules/LoggingBehaviorTests.cs` — `LoggingBehavior` pipeline: logs request entry and exit; exception in `next()` is logged and re-thrown
- [X] T035 [P] [US1] Expand `tests/Vendor.Application.Tests/Validators/` — add validator tests for `PlaceOrderCommand` (null customer, empty lines rejected), `RegisterCustomerCommand` (invalid email format rejected), `UpdateVendorSettingsCommand` (invalid branding config rejected)

### Infrastructure Tests (Vendor.Infrastructure.Tests) — Testcontainers

- [X] T036 [US1] Annotate `tests/Vendor.Infrastructure.Tests/Persistence/VendorSettingsRepositoryTests.cs` with `[Collection("Database")]`; rewrite to use `MsSqlFixture` connection string (replacing EF InMemory); add `await Fixture.ResetAsync()` in `InitializeAsync`; test `SaveAsync`→`GetAsync` roundtrip with real MSSQL
- [X] T037 [US1] Annotate `tests/Vendor.Infrastructure.Tests/Persistence/DbContextTests.cs` with `[Collection("Database")]`; rewrite with real MSSQL; test `VendorDbContext` migrations applied, owned type columns present in schema, outbox table exists
- [X] T038 [US1] Create `tests/Vendor.Infrastructure.Tests/Persistence/RepositoriesIntegrationTests.cs` — `[Collection("Database")]`; test `CustomerRepository.AddAsync`/`GetByIdAsync`/`GetByEmailAsync`, `ProductRepository.AddAsync`/`GetBySlugAsync`, `OrderRepository.AddAsync`/`GetByIdAsync`/`GetCustomerIdAsync`; all using real MSSQL via `MsSqlFixture`
- [X] T039 [US1] Create `tests/Vendor.Infrastructure.Tests/Outbox/OutboxProcessorTests.cs` — `[Collection("Database")]`; insert `OutboxMessage` rows directly; run `OutboxProcessorHostedService` for one polling cycle; assert rows marked `ProcessedOn` and domain events dispatched via mock `IPublisher`
- [X] T040 [P] [US1] Create `tests/Vendor.Infrastructure.Tests/Auth/SecretResolverTests.cs` — unit tests (no container needed): `ref:env:VAR` resolved from environment variable; `ref:env:MISSING` throws `SecretResolutionException`; non-`ref:` value throws on strict mode
- [X] T041 [P] [US1] Create `tests/Vendor.Infrastructure.Tests/Config/VendorConfigValidationFilterTests.cs` — unit test: valid `VendorConfig` passes `ValidateAndThrow()`; missing required fields throws `ValidationException`; `SecretResolutionFilter` runs before `VendorConfigValidationFilter`
- [X] T042 [P] [US1] Create `tests/Vendor.Infrastructure.Tests/Payments/PaymentGatewayFactoryTests.cs` — `GetPaymentGateway("stripe")` returns `StripePaymentGateway`; unknown provider throws `ArgumentException`; factory respects config-driven adapter selection
- [X] T043 [P] [US1] Expand `tests/Vendor.Infrastructure.Tests/Analytics/` — `AnalyticsService` routes events to configured provider (mock); disabled analytics config skips dispatch

### API Tests (Vendor.Api.Tests) — WebApplicationFactory

- [X] T044 [US1] Rewrite `tests/Vendor.Api.Tests/Integration/AuthEndpointsTests.cs` to use `VendorApiFactory` + `AuthHelper`; test `POST /api/v1/auth/register` (201 Created), `POST /api/v1/auth/login` (200 + JWT), `POST /api/v1/auth/refresh` (200 + new JWT), invalid credentials (401)
- [X] T045 [US1] Rewrite `tests/Vendor.Api.Tests/Integration/ProductEndpointsTests.cs`; test `GET /api/v1/products` (200), `GET /api/v1/products/{id}` (200 + 404), `POST /api/v1/products` with admin JWT (201), without auth (401), with customer JWT (403)
- [X] T046 [US1] Rewrite `tests/Vendor.Api.Tests/Integration/CartEndpointsTests.cs`; test `POST /api/v1/cart/items` with customer JWT (200), without auth (401), `DELETE /api/v1/cart/items/{id}` (204)
- [X] T047 [US1] Rewrite `tests/Vendor.Api.Tests/Integration/OrderAndPaymentEndpointsTests.cs`; test `POST /api/v1/orders` (201 → order placed), `GET /api/v1/orders/{id}` (200 owned by customer, 403 for other customer), `POST /api/v1/orders/{id}/pay` (200)
- [X] T048 [US1] Rewrite `tests/Vendor.Api.Tests/Integration/AdminAndHealthEndpointsTests.cs`; test `GET /health/live` (200 no auth), `GET /health/ready` (200 no auth), `GET /api/v1/admin/config` with admin JWT (200), with customer JWT (403), without auth (401)
- [X] T049 [US1] Rewrite `tests/Vendor.Api.Tests/Integration/PipelineIntegrationTests.cs`; test `X-Correlation-ID` header echoed in response, security headers present (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`), rate limiting returns 429 after burst, maintenance mode returns 503 for non-health endpoints when flag on
- [X] T050 [US1] Create `tests/Vendor.Api.Tests/Unit/GlobalExceptionHandlerTests.cs` — test unhandled exception produces RFC 7807 `ProblemDetails` with `status: 500`; `BusinessRuleViolationException` maps to 422; `InvalidStateTransitionException` maps to 409
- [X] T051 [US1] Create `tests/Vendor.Api.Tests/Unit/MiddlewareTests.cs` — unit tests for `SecurityHeadersMiddleware` (all 5 headers present), `CorrelationIdMiddleware` (generates when missing, propagates when present), `MaintenanceModeMiddleware` (returns 503 on flag; health routes pass through)

**Checkpoint**: US1 complete — run `dotnet test` and `reportgenerator`; verify Domain ≥90%, Application ≥85%, Infrastructure ≥70%, API ≥75%, overall ≥80%.

---

## Phase 4: User Story 2 — PR Validation & Build/Test CI Pipeline (Priority: P1)

**Goal**: GitHub Actions workflow with 3 sequential PR jobs (validate → build-test → docker) gating all PRs. Existing `validate-vendor-config.yml` superseded.

**Independent Test**: Open a test PR; verify all three jobs run in order; introduce a raw secret into `vendor.config.json` to confirm Stage 1 blocks merge; reduce test coverage to verify Stage 2 coverage gate fails.

### Implementation for User Story 2

- [X] T052 [US2] Create `.hadolint.yaml` at repository root — configure `failure-threshold: warning`; add `ignore: [DL3008]` for acceptable build-stage rule suppressions; document choices with inline comments
- [X] T053 [US2] Create `Dockerfile` at repository root — Stage 1: `FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build`, `WORKDIR /src`, `COPY` solution + csproj files, `RUN dotnet restore`, `COPY` source, `RUN dotnet publish src/Vendor.Api/Vendor.Api.csproj -c Release -o /app/publish`; Stage 2: `FROM mcr.microsoft.com/dotnet/aspnet:9.0`, `WORKDIR /app`, `COPY --from=build /app/publish .`, `ENV ASPNETCORE_URLS=http://+:8080`, `USER appuser:appgroup`, `EXPOSE 8080`, `HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 CMD curl --fail http://localhost:8080/health/live || exit 1`, `ENTRYPOINT ["dotnet", "Vendor.Api.dll"]`
- [X] T054 [US2] Create `.github/workflows/ci-cd.yml` — `on: pull_request` (all branches) and `on: push` (branches: [develop, main]); define `validate` job with 3 steps: ① `actions/setup-node@v4` (Node 20) + `npx ajv-cli validate -s config/vendor.config.schema.json -d config/vendor.config.json --spec=draft2020`; ② `node scripts/audit-secrets.js config/vendor.config.json`; ③ `hadolint/hadolint-action@v3.1.0` targeting `Dockerfile`
- [X] T055 [US2] Add `build-test` job to `.github/workflows/ci-cd.yml` — `needs: [validate]`; steps: `actions/setup-dotnet@v4` (9.0.x), `dotnet restore Vendor.slnx`, `dotnet build Vendor.slnx --no-restore -c Release`, `dotnet test Vendor.slnx --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./coverage --logger "console;verbosity=normal"`, followed by coverage summary gate (≥80%)
- [X] T056 [US2] Add `docker` job to `.github/workflows/ci-cd.yml` — `needs: [build-test]`; `if: github.event_name == 'push'`; steps: `docker/login-action@v3` (registry `ghcr.io`, username `${{ github.actor }}`, password `${{ secrets.GITHUB_TOKEN }}`), `docker/metadata-action@v5`, `docker/build-push-action@v5`

**Checkpoint**: US2 complete — push branch with intentional schema error; confirm validate job fails; fix and push; confirm all 3 jobs green; PR CI gating confirmed.

---

## Phase 5: User Story 3 — Multi-Stage Containerization & Vendor Mount Isolation (Priority: P2)

**Goal**: Production-ready Dockerfile validated locally; container starts as non-root on port 8080; `/health/live` responds 200 OK; vendor config/theme updated via mounts without image rebuild.

- [X] T057 [US3] Verify `Dockerfile` builds locally with `docker build -t vendor-api:local .` — fix any layer-ordering issues; ensure `.dockerignore` excludes `bin/`, `obj/`, `.git/`, `specs/`, `tests/`
- [X] T058 [US3] Create `.dockerignore` at repository root — exclude: `**/bin/`, `**/obj/`, `.git`, `.github`, `specs/`, `tests/`, `docs/`, `*.md`
- [X] T059 [US3] Validate non-root execution: `appuser:appgroup` (UID 10001)
- [X] T060 [US3] Validate volume hot-swap: run container with mounted `./local-config`; edit `vendor.config.json`
- [X] T061 [US3] Run `hadolint Dockerfile` locally — resolve any remaining lint warnings

**Checkpoint**: US3 complete — Dockerfile passes hadolint, builds in <2 minutes (cached), container starts as non-root, health probe responds.

---

## Phase 6: User Story 4 — Staging & Production Deployment Pipelines (Priority: P2)

**Goal**: Staging auto-deploys on merge to `develop` with smoke tests; Production requires manual approval on merge to `main` with post-deploy health check.

- [X] T062 [US4] Add `deploy-staging` job to `.github/workflows/ci-cd.yml` — `needs: [docker]`; `if: github.ref == 'refs/heads/develop' && github.event_name == 'push'`; environment: staging; smoke test step
- [X] T063 [US4] Add `deploy-production` job to `.github/workflows/ci-cd.yml` — `needs: [docker]`; `if: github.ref == 'refs/heads/main' && github.event_name == 'push'`; environment: production; post-deploy health check step
- [X] T064 [US4] Configure GitHub Environment `production` in repository settings
- [X] T065 [US4] Create `docs/ci-cd.md` documentation

**Checkpoint**: US4 complete — full 5-stage pipeline operational.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T066 Run full `dotnet test Vendor.slnx --collect:"XPlat Code Coverage"`; confirm overall line coverage ≥80% and per-layer targets (Domain ≥90%, Application ≥85%, Infrastructure ≥70%, API ≥75%)
- [X] T067 [P] Delete placeholder test files if present
- [X] T068 [P] Run secret audit and JSON schema validation
- [X] T069 [P] Run hadolint Dockerfile lint check
- [X] T070 [P] Execute validation scenarios
- [X] T071 Update quickstart.md documentation
- [X] T072 Mark all tasks in this file complete `[X]`
