# Implementation Plan: Vendor Configuration System

**Branch**: `001-vendor-config-system` | **Date**: 2026-07-25 | **Spec**: [spec.md](file:///C:/Users/c/Desktop/Work/e-commerce-prototype/specs/001-vendor-config-system/spec.md)

**Input**: Feature specification from `specs/001-vendor-config-system/spec.md`

## Summary

Build the vendor configuration system that enables a single codebase to serve
different vendor deployments with zero code changes. Configuration resolves
across three strict tiers: build-time (`vendorId`, locked at deploy), boot-time
(secrets, auth, caching, email provider — validated via FluentValidation inside
an `IStartupFilter`, crashes on failure), and runtime (branding, checkout rules,
feature flags, payment/shipping settings — editable via Admin API, persisted to
MSSQL `VendorSettings` table). CI validation uses `ajv-cli` for JSON Schema
checks and a custom `audit-secrets.js` script in GitHub Actions.

## Technical Context

**Language/Version**: C# / .NET 9

**Primary Dependencies**: MediatR, FluentValidation, Entity Framework Core,
System.Text.Json, ajv-cli (npm, CI only)

**Storage**: Microsoft SQL Server (MSSQL) — single database per vendor deployment

**Testing**: xUnit, FluentAssertions, NSubstitute, Testcontainers (MSSQL),
`WebApplicationFactory<Program>`

**Target Platform**: Linux container (Docker) / Windows Server

**Project Type**: Web service (ASP.NET Minimal API backend)

**Performance Goals**: Boot validation + secret resolution < 2s; runtime
config patch response < 100ms

**Constraints**: Domain layer has zero external NuGet dependencies; all
vendor-specific behavior driven by `config/vendor.config.json` + `theme/`;
zero C# or React code changes to onboard a new vendor

**Scale/Scope**: Single-tenant, single-instance per vendor; 14 configuration
sections; 3 Admin API endpoints

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Clean Architecture** | ✅ PASS | `VendorConfig` aggregate + value objects in Domain (zero NuGet deps). FluentValidation validators in Application. EF Core `VendorSettings` persistence + `SecretResolver` in Infrastructure. Minimal API endpoints in API (composition root). |
| **II. Result\<T\> Handlers** | ✅ PASS | `UpdateVendorSettingsCommand` and `GetVendorConfigQuery` handlers return `Result<T>`. Concurrency conflicts → `Result.Failure(ConflictError)`. Validation failures → `Result.Failure(ValidationError)`. |
| **III. EF Core Owned Types** | ✅ PASS | `VendorSettings` entity uses `IEntityTypeConfiguration<VendorSettings>` in Infrastructure. Runtime config stored as JSON column in the `VendorSettings` table. Domain value objects (`BrandingConfig`, `LocaleConfig`, etc.) have zero EF Core dependency. |
| **IV. Clone-Per-Vendor** | ✅ PASS | All vendor-variable behavior driven by `config/vendor.config.json` + `theme/`. No conditional code branches keyed on vendor identity. Entire config system is config-driven. |
| **V. Secrets Reference-Only** | ✅ PASS | `SecretReference` value object enforces `^ref:(env\|vault\|aws-ssm):.+$` pattern at construction. `SecretResolver` resolves at boot. CI `audit-secrets.js` script fails builds on raw secrets. |
| **VI. Transactional Outbox** | ✅ PASS | `VendorSettingsUpdatedEvent` raised via outbox when Admin API updates runtime config. Event inserted into `OutboxMessages` table in the same `SaveChangesAsync` transaction. |
| **VII. Test Coverage** | ✅ PASS | Domain value objects + validation rules target 90% coverage. Application handlers target 85%. Infrastructure (EF Core config, secret resolver) target 70%. API endpoints target 75%. |

**Gate result**: All 7 principles pass. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/001-vendor-config-system/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── admin-config-api.md       # Admin API contract
│   └── ci-validation-contract.md # CI pipeline contract
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
e-commerce-prototype/
│
├── config/
│   ├── vendor.config.json              # Vendor-specific configuration
│   └── vendor.config.schema.json       # JSON Schema (Draft 2020-12)
│
├── scripts/
│   ├── audit-secrets.js                # CI secret-reference audit script
│   └── secret-fields.json              # Manifest of secret JSON paths
│
├── src/
│   ├── Vendor.Domain/
│   │   ├── Aggregates/
│   │   │   └── VendorSettings/
│   │   │       ├── VendorConfig.cs           # Aggregate root (composite)
│   │   │       ├── VendorBuildConfig.cs       # Build-tier value object
│   │   │       ├── VendorBootConfig.cs        # Boot-tier value object
│   │   │       └── VendorRuntimeConfig.cs     # Runtime-tier value object
│   │   ├── ValueObjects/
│   │   │   ├── SecretReference.cs             # ref:* pattern enforcement
│   │   │   ├── BrandingConfig.cs
│   │   │   ├── LocaleConfig.cs
│   │   │   ├── TaxConfig.cs
│   │   │   ├── CheckoutConfig.cs
│   │   │   ├── AuthConfig.cs
│   │   │   ├── CachingConfig.cs
│   │   │   ├── EmailConfig.cs
│   │   │   ├── AnalyticsConfig.cs
│   │   │   ├── PromotionsConfig.cs
│   │   │   ├── FeatureFlags.cs
│   │   │   ├── PaymentProviderConfig.cs
│   │   │   ├── PaymentCredentials.cs
│   │   │   └── ShippingProviderConfig.cs
│   │   ├── Enums/
│   │   │   ├── CacheProvider.cs
│   │   │   ├── EmailProvider.cs
│   │   │   ├── TaxStrategy.cs
│   │   │   ├── TextDirection.cs
│   │   │   ├── CaptureMode.cs
│   │   │   └── SecretBackend.cs
│   │   ├── Events/
│   │   │   └── VendorSettingsUpdatedEvent.cs
│   │   └── Interfaces/
│   │       ├── ISecretResolver.cs
│   │       └── IVendorSettingsRepository.cs
│   │
│   ├── Vendor.Application/
│   │   ├── Commands/
│   │   │   └── VendorSettings/
│   │   │       ├── UpdateVendorSettingsCommand.cs
│   │   │       └── UpdateVendorSettingsHandler.cs
│   │   ├── Queries/
│   │   │   └── VendorSettings/
│   │   │       ├── GetVendorConfigQuery.cs
│   │   │       └── GetVendorConfigHandler.cs
│   │   ├── DTOs/
│   │   │   ├── VendorConfigDto.cs
│   │   │   └── VendorConfigPatchDto.cs
│   │   └── Validators/
│   │       ├── VendorConfigValidator.cs         # Full config validation
│   │       └── VendorConfigPatchValidator.cs     # Patch-specific validation
│   │
│   ├── Vendor.Infrastructure/
│   │   ├── Config/
│   │   │   ├── SecretResolutionFilter.cs         # IStartupFilter #1
│   │   │   ├── VendorConfigValidationFilter.cs   # IStartupFilter #2
│   │   │   ├── EnvironmentSecretResolver.cs
│   │   │   ├── VaultSecretResolver.cs
│   │   │   ├── AwsSsmSecretResolver.cs
│   │   │   ├── CompositeSecretResolver.cs        # Strategy dispatcher
│   │   │   └── ResolvedSecretStore.cs            # In-memory secret cache
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   │   └── VendorSettingsConfiguration.cs
│   │   │   └── Repositories/
│   │   │       └── VendorSettingsRepository.cs
│   │   └── DependencyInjection.cs
│   │
│   └── Vendor.Api/
│       ├── Program.cs
│       ├── Endpoints/
│       │   └── VendorSettingsEndpoints.cs
│       └── Filters/
│           └── ResultEndpointFilter.cs
│
├── tests/
│   ├── Vendor.Domain.Tests/
│   │   └── ValueObjects/
│   │       ├── SecretReferenceTests.cs
│   │       ├── LocaleConfigTests.cs
│   │       ├── CheckoutConfigTests.cs
│   │       └── BrandingConfigTests.cs
│   ├── Vendor.Application.Tests/
│   │   ├── Validators/
│   │   │   └── VendorConfigValidatorTests.cs
│   │   └── Handlers/
│   │       ├── UpdateVendorSettingsHandlerTests.cs
│   │       └── GetVendorConfigHandlerTests.cs
│   ├── Vendor.Infrastructure.Tests/
│   │   ├── Config/
│   │   │   ├── SecretResolutionFilterTests.cs
│   │   │   └── CompositeSecretResolverTests.cs
│   │   └── Persistence/
│   │       └── VendorSettingsRepositoryTests.cs
│   └── Vendor.Api.Tests/
│       └── Endpoints/
│           └── VendorSettingsEndpointTests.cs
│
├── .github/
│   └── workflows/
│       └── validate-vendor-config.yml
│
└── Vendor.sln
```

**Structure Decision**: Clean Architecture four-project layout as defined in
the architecture blueprint. This feature adds files across all four layers
plus CI tooling (`scripts/`, `.github/workflows/`) and the vendor config
file (`config/`). No new projects are introduced.

## Complexity Tracking

> No constitution violations. No complexity justifications needed.
