# Implementation Plan: API Layer Composition Root & REST Endpoints

**Branch**: `005-api-layer-rest-endpoints` | **Date**: 2026-07-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-api-layer-rest-endpoints/spec.md`

## Summary

Build `Vendor.Api` — the ASP.NET Core 9 Minimal API composition root that:
- Registers all Application and Infrastructure services via DI extension methods
- Exposes 63 REST endpoints in 9 module groups + SignalR hub + 2 health probes
- Enforces a 9-stage ordered middleware pipeline (exception handler → security headers → correlation ID → Serilog request logging → response compression → CORS → rate limiting → maintenance mode → auth)
- Uses Asp.Versioning.Http 8.x for URL-segment versioning at `/api/v{version:apiVersion}/`
- Enforces 4 named fixed-window rate limit policies via built-in `Microsoft.AspNetCore.RateLimiting`
- Integrates the `AdminNotificationHub` SignalR endpoint with JWT query-string auth
- Exposes `/health/live` (liveness) and `/health/ready` (MSSQL + Redis + Payment Gateway readiness)

## Technical Context

**Language/Version**: C# 13 / .NET 9.0

**Primary Dependencies**:
- `Asp.Versioning.Http` 8.x — URL-segment API versioning for Minimal APIs
- `Serilog.AspNetCore` — structured HTTP request logging
- `Serilog.Sinks.Console` + `Serilog.Sinks.Seq` — log output
- `Microsoft.AspNetCore.RateLimiting` (built-in) — 4 named fixed-window policies
- `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` or `Swashbuckle.AspNetCore` — OpenAPI v3 docs
- `Microsoft.AspNetCore.Diagnostics.HealthChecks` (built-in) — liveness + readiness probes
- `Microsoft.AspNetCore.SignalR` (built-in) — AdminNotificationHub

**Storage**: MSSQL via EF Core 9 (VendorDbContext — registered in Infrastructure layer)

**Testing**: xUnit + `WebApplicationFactory<Program>` for API integration tests (75% minimum coverage per constitution)

**Target Platform**: ASP.NET Core 9 on Linux container / Windows Server

**Project Type**: Web service — Minimal API composition root (no business logic)

**Performance Goals**: P95 response time < 500ms for catalog endpoints; < 200ms for auth token refresh

**Constraints**: API layer MUST NOT contain business logic per constitution Principle I

**Scale/Scope**: ~63 endpoints serving single-tenant deployment; horizontal scaling via Redis backplane for SignalR

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I — Clean Architecture (inward-only deps) | ✅ PASS | `Vendor.Api` references only `Vendor.Application` and `Vendor.Infrastructure`; no domain logic in API layer |
| II — Result-Oriented Handlers | ✅ PASS | All endpoint handlers call MediatR `Send()` and map `Result<T>` to HTTP via `ToHttpResult()` extension; no thrown exceptions |
| III — MSSQL / EF Core Owned Types | ✅ PASS | No data access in API layer; delegated entirely to Infrastructure |
| IV — Clone-Per-Vendor Isolation | ✅ PASS | CORS origins, rate limit thresholds, and payment gateway config are read from `VendorRuntimeConfig`; no hardcoded vendor identity |
| V — Secrets Reference-Only | ✅ PASS | JWT signing key, Seq URL, CORS secrets all read via `ref:env:` / `ref:vault:` through `SecretResolver` |
| VI — Domain Events via Outbox | ✅ PASS | API layer does not dispatch domain events directly; handled by Infrastructure OutboxProcessor |
| VII — Test Coverage (API ≥ 75%) | ✅ PLAN | `Vendor.Api.Tests` using `WebApplicationFactory<Program>` targeting 75% coverage |

## Project Structure

### Documentation (this feature)

```text
specs/005-api-layer-rest-endpoints/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code

```text
src/
└── Vendor.Api/
    ├── Vendor.Api.csproj
    ├── Program.cs                          # Composition root, pipeline, DI wiring
    ├── appsettings.json                    # Non-secret config defaults
    ├── appsettings.Development.json
    ├── Endpoints/
    │   ├── AuthEndpoints.cs                # 9 auth endpoints
    │   ├── ProductEndpoints.cs             # 13 product endpoints
    │   ├── CartEndpoints.cs                # 7 cart endpoints + checkout
    │   ├── OrderEndpoints.cs               # 8 order endpoints
    │   ├── PaymentEndpoints.cs             # 4 payment + 4 webhook endpoints
    │   ├── ShipmentEndpoints.cs            # 6 shipment endpoints
    │   ├── ReturnEndpoints.cs              # 8 return endpoints
    │   ├── PromotionEndpoints.cs           # 4 promotion endpoints
    │   └── AdminEndpoints.cs              # 11 analytics/settings/customer endpoints
    ├── Middleware/
    │   ├── GlobalExceptionHandler.cs       # IExceptionHandler → ProblemDetails RFC 7807
    │   ├── SecurityHeadersMiddleware.cs    # Stage 2 security headers
    │   ├── CorrelationIdMiddleware.cs      # Stage 3 X-Correlation-ID
    │   └── MaintenanceModeMiddleware.cs    # Stage 8 503 short-circuit
    ├── Extensions/
    │   ├── ResultExtensions.cs             # Result<T> → IResult (TypedResults.Problem)
    │   ├── ServiceExtensions.cs            # AddApplicationServices, AddInfrastructureServices wrappers
    │   └── WebApplicationExtensions.cs     # MapAllEndpoints() convenience method
    └── HealthChecks/
        ├── RedisHealthCheck.cs             # IConnectionMultiplexer PingAsync
        └── PaymentGatewayHealthCheck.cs    # Config validation health check

tests/
└── Vendor.Api.Tests/
    ├── Vendor.Api.Tests.csproj
    ├── Integration/
    │   ├── AuthEndpointTests.cs
    │   ├── ProductEndpointTests.cs
    │   ├── CartEndpointTests.cs
    │   ├── OrderEndpointTests.cs
    │   └── HealthCheckTests.cs
    └── Unit/
        ├── GlobalExceptionHandlerTests.cs
        ├── ResultExtensionsTests.cs
        └── MaintenanceModeMiddlewareTests.cs
```

**Structure Decision**: Single Minimal API project (`Vendor.Api`) with endpoint modules in `Endpoints/` subdirectory. One file per functional area with extension methods on `IEndpointRouteBuilder`. Middleware in `Middleware/`. Health checks in `HealthChecks/`.
