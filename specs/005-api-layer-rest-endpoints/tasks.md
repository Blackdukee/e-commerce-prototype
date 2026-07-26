# Tasks: API Layer Composition Root & REST Endpoints

**Input**: Design documents from `specs/005-api-layer-rest-endpoints/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/api-endpoint-registry.md`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5, US6)
- File paths are explicitly specified in task descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and project references

- [X] T001 Create `Vendor.Api` ASP.NET Core project in `src/Vendor.Api/Vendor.Api.csproj` with references to `Vendor.Application` and `Vendor.Infrastructure`
- [X] T002 Add NuGet package references (`Asp.Versioning.Http`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.Seq`, `Serilog.Enrichers.CorrelationId`, `Swashbuckle.AspNetCore`) to `src/Vendor.Api/Vendor.Api.csproj`
- [X] T003 [P] Create `Vendor.Api.Tests` project in `tests/Vendor.Api.Tests/Vendor.Api.Tests.csproj` with references to `Vendor.Api`, `Microsoft.AspNetCore.Mvc.Testing`, `FluentAssertions`, `NSubstitute`, and `xunit`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core HTTP extensions, middleware components, and error mapping required by all REST endpoints

**⚠️ CRITICAL**: Must be complete before endpoint routes can be executed

- [X] T004 Create `ResultExtensions.cs` in `src/Vendor.Api/Extensions/ResultExtensions.cs` to map MediatR `Result<T>` and `Result` failure variants to RFC 7807 `ProblemDetails` (`TypedResults.Problem`)
- [X] T005 [P] Create `GlobalExceptionHandler.cs` in `src/Vendor.Api/Middleware/GlobalExceptionHandler.cs` implementing `IExceptionHandler` for RFC 7807 exception handling
- [X] T006 [P] Create `SecurityHeadersMiddleware.cs` in `src/Vendor.Api/Middleware/SecurityHeadersMiddleware.cs` to set `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, and `Referrer-Policy` headers
- [X] T007 [P] Create `CorrelationIdMiddleware.cs` in `src/Vendor.Api/Middleware/CorrelationIdMiddleware.cs` to propagate `X-Correlation-ID` header and push to Serilog `LogContext`
- [X] T008 [P] Create `MaintenanceModeMiddleware.cs` in `src/Vendor.Api/Middleware/MaintenanceModeMiddleware.cs` returning 503 Service Unavailable for non-exempt routes when maintenance mode feature flag is active
- [X] T009 [P] Create unit tests for `GlobalExceptionHandler`, `ResultExtensions`, and `MaintenanceModeMiddleware` in `tests/Vendor.Api.Tests/Unit/MiddlewareTests.cs`

**Checkpoint**: Foundation ready — REST endpoint module implementation can begin

---

## Phase 3: User Story 1 — API Composition Root & Ordered Middleware Pipeline (Priority: P1) 🎯 MVP

**Goal**: Bootstrap ASP.NET Core host with ordered 9-stage pipeline, Serilog logging, API versioning, rate limiting, and OpenAPI swagger docs

**Independent Test**: Execute `tests/Vendor.Api.Tests/Integration/PipelineIntegrationTests.cs` using `WebApplicationFactory<Program>` to verify pipeline order, response headers, and `/swagger` OpenAPI generation.

- [X] T010 [US1] Create non-secret defaults in `src/Vendor.Api/appsettings.json` and `src/Vendor.Api/appsettings.Development.json`
- [X] T011 [US1] Implement `ServiceExtensions.cs` in `src/Vendor.Api/Extensions/ServiceExtensions.cs` to wire `AddApplicationServices()`, `AddInfrastructureServices()`, rate limiters (4 named policies), API versioning, and CORS
- [X] T012 [US1] Implement `Program.cs` composition root in `src/Vendor.Api/Program.cs` configuring the ordered 9-stage pipeline and bootstrapped Serilog logging
- [X] T013 [P] [US1] Implement integration tests for composition root and pipeline in `tests/Vendor.Api.Tests/Integration/PipelineIntegrationTests.cs`

**Checkpoint**: User Story 1 complete — API composition root bootstraps cleanly with full middleware protection

---

## Phase 4: User Story 2 — Auth & Customer Profile Endpoints (Priority: P1)

**Goal**: Expose 9 Auth REST endpoints and 4 Customer profile/consent endpoints

**Independent Test**: Execute `tests/Vendor.Api.Tests/Integration/AuthEndpointsTests.cs` testing customer registration, JWT login, guest session, token refresh, profile query, and consent update.

- [X] T014 [P] [US2] Create DTO request and response records for Auth and Customer in `src/Vendor.Api/DTOs/AuthDtos.cs` and `src/Vendor.Api/DTOs/CustomerDtos.cs`
- [X] T015 [US2] Implement `AuthEndpoints.cs` in `src/Vendor.Api/Endpoints/AuthEndpoints.cs` mapping 9 Auth endpoints under `/api/v1/auth/`
- [X] T016 [US2] Implement `CustomerEndpoints.cs` in `src/Vendor.Api/Endpoints/CustomerEndpoints.cs` mapping 4 Customer endpoints under `/api/v1/customer/`
- [X] T017 [P] [US2] Implement integration tests for Auth and Customer endpoints in `tests/Vendor.Api.Tests/Integration/AuthEndpointsTests.cs`

**Checkpoint**: User Story 2 complete — User authentication, tokens, and customer profiles fully accessible via REST

---

## Phase 5: User Story 3 — Product Catalog & Admin Inventory Endpoints (Priority: P1)

**Goal**: Expose 13 public and administrative REST endpoints for products, variants, stock, images, and activation

**Independent Test**: Execute `tests/Vendor.Api.Tests/Integration/ProductEndpointsTests.cs` testing public product browsing by slug and admin CRUD/stock management.

- [X] T018 [P] [US3] Create DTO request and response records for Products and Variants in `src/Vendor.Api/DTOs/ProductDtos.cs`
- [X] T019 [US3] Implement `ProductEndpoints.cs` in `src/Vendor.Api/Endpoints/ProductEndpoints.cs` mapping 13 product endpoints under `/api/v1/products/` and `/api/v1/admin/products/`
- [X] T020 [P] [US3] Implement integration tests for Product endpoints in `tests/Vendor.Api.Tests/Integration/ProductEndpointsTests.cs`

**Checkpoint**: User Story 3 complete — Product catalog browsing and inventory management fully functional

---

## Phase 6: User Story 4 — Shopping Cart & Checkout Endpoints (Priority: P1)

**Goal**: Expose 7 Cart REST endpoints and 2-phase Checkout initiation endpoint

**Independent Test**: Execute `tests/Vendor.Api.Tests/Integration/CartEndpointsTests.cs` testing cart CRUD, discount application, guest cart merge, and checkout execution.

- [X] T021 [P] [US4] Create DTO request and response records for Cart and Checkout in `src/Vendor.Api/DTOs/CartDtos.cs`
- [X] T022 [US4] Implement `CartEndpoints.cs` in `src/Vendor.Api/Endpoints/CartEndpoints.cs` mapping 7 cart endpoints and checkout orchestrator under `/api/v1/cart/` and `/api/v1/orders/checkout`
- [X] T023 [P] [US4] Implement integration tests for Cart and Checkout endpoints in `tests/Vendor.Api.Tests/Integration/CartEndpointsTests.cs`

**Checkpoint**: User Story 4 complete — Shopping cart management and two-phase checkout orchestration operational

---

## Phase 7: User Story 5 — Orders, Payments, Webhooks, Shipments & Returns Endpoints (Priority: P2)

**Goal**: Expose 8 Order endpoints, 8 Payment & Webhook endpoints, 6 Shipment endpoints, and 8 Return endpoints

**Independent Test**: Execute `tests/Vendor.Api.Tests/Integration/OrderAndPaymentEndpointsTests.cs` testing order lifecycle, payment captures/webhooks, shipping rate calculations, and return workflows.

- [X] T024 [P] [US5] Create DTO request and response records for Orders, Payments, Shipments, and Returns in `src/Vendor.Api/DTOs/OrderDtos.cs`, `src/Vendor.Api/DTOs/PaymentDtos.cs`, `src/Vendor.Api/DTOs/ShipmentDtos.cs`, and `src/Vendor.Api/DTOs/ReturnDtos.cs`
- [X] T025 [US5] Implement `OrderEndpoints.cs` in `src/Vendor.Api/Endpoints/OrderEndpoints.cs` mapping 8 Order endpoints under `/api/v1/orders/` and `/api/v1/admin/orders/`
- [X] T026 [US5] Implement `PaymentEndpoints.cs` in `src/Vendor.Api/Endpoints/PaymentEndpoints.cs` mapping 4 Payment endpoints and 4 Webhooks (Stripe, PayPal, Paymob, Shipping) under `/api/v1/payments/`, `/api/v1/admin/payments/`, and `/api/v1/webhooks/`
- [X] T027 [US5] Implement `ShipmentEndpoints.cs` in `src/Vendor.Api/Endpoints/ShipmentEndpoints.cs` mapping 6 Shipment endpoints under `/api/v1/shipments/` and `/api/v1/admin/shipments/`
- [X] T028 [US5] Implement `ReturnEndpoints.cs` in `src/Vendor.Api/Endpoints/ReturnEndpoints.cs` mapping 8 Return endpoints under `/api/v1/returns/` and `/api/v1/admin/returns/`
- [X] T029 [P] [US5] Implement integration tests for Orders, Payments, Webhooks, Shipments, and Returns in `tests/Vendor.Api.Tests/Integration/OrderAndPaymentEndpointsTests.cs`

**Checkpoint**: User Story 5 complete — Full fulfillment cycle (Orders, Payments, Webhooks, Shipments, Returns) accessible

---

## Phase 8: User Story 6 — Promotions, Analytics, Admin Settings, SignalR & Health Checks (Priority: P2)

**Goal**: Expose 4 Promotion endpoints, 7 Settings/Analytics endpoints, SignalR AdminHub, and Liveness/Readiness health probes

**Independent Test**: Execute `tests/Vendor.Api.Tests/Integration/AdminAndHealthEndpointsTests.cs` testing promotion validation, runtime settings patches, SignalR JWT handshake, and `/health/ready` probe output.

- [X] T030 [P] [US6] Create DTO request and response records for Promotions, Analytics, and Settings in `src/Vendor.Api/DTOs/PromotionDtos.cs` and `src/Vendor.Api/DTOs/AdminDtos.cs`
- [X] T031 [P] [US6] Implement `RedisHealthCheck.cs` and `PaymentGatewayHealthCheck.cs` in `src/Vendor.Api/HealthChecks/HealthChecks.cs`
- [X] T032 [US6] Implement `PromotionEndpoints.cs` in `src/Vendor.Api/Endpoints/PromotionEndpoints.cs` mapping 4 Promotion endpoints under `/api/v1/promotions/` and `/api/v1/admin/promotions/`
- [X] T033 [US6] Implement `AdminEndpoints.cs` in `src/Vendor.Api/Endpoints/AdminEndpoints.cs` mapping 7 Analytics and Runtime Settings endpoints under `/api/v1/admin/`
- [X] T034 [US6] Map `/hubs/admin` SignalR endpoint and `/health/live`, `/health/ready` probes in `src/Vendor.Api/Extensions/WebApplicationExtensions.cs`
- [X] T035 [P] [US6] Implement integration tests for Promotions, Settings, Health Probes, and SignalR Handshake in `tests/Vendor.Api.Tests/Integration/AdminAndHealthEndpointsTests.cs`

**Checkpoint**: User Story 6 complete — Administrative tools, real-time push notifications, and monitoring probes active

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Solution integration and overall test suite verification

- [X] T036 [P] Update solution file `Vendor.slnx` to include `src/Vendor.Api/Vendor.Api.csproj` and `tests/Vendor.Api.Tests/Vendor.Api.Tests.csproj`
- [X] T037 Execute full test suite (`dotnet test`) across `Vendor.Domain.Tests`, `Vendor.Application.Tests`, `Vendor.Infrastructure.Tests`, and `Vendor.Api.Tests` verifying 100% test pass rate and ≥75% API layer coverage
