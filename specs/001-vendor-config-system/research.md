# Research: Vendor Configuration System

**Feature**: 001-vendor-config-system
**Date**: 2026-07-25

## R1: Boot-Time Validation via IStartupFilter + FluentValidation

**Decision**: Use `IStartupFilter` to run FluentValidation validators against `VendorConfig` at application startup, before the first HTTP request is served.

**Rationale**: `IStartupFilter` executes during host build, after DI container is configured but before Kestrel begins accepting connections. This guarantees that invalid configuration crashes the container before any traffic arrives. FluentValidation provides strongly-typed, composable rule chains that are unit-testable independently of the ASP.NET pipeline.

**Alternatives considered**:
- `IHostedService.StartAsync` — executes too late (after Kestrel starts listening); risk of serving requests with invalid config.
- `IOptions<T>.Validate()` with `ValidateOnStart()` — lightweight but lacks the expressiveness of FluentValidation for complex cross-section rules (e.g., "defaultCurrency must be in supportedCurrencies").
- Custom middleware — runs per-request, not once at boot; wrong lifecycle.

**Implementation notes**:
- Register `IStartupFilter` implementation named `VendorConfigValidationFilter`.
- Inject `IValidator<VendorConfig>` and call `ValidateAndThrow()`. If validation fails, the exception propagates up and halts the host.
- The filter runs after the `SecretResolver` IStartupFilter (ordering matters: resolve secrets first, then validate the fully-resolved config).

## R2: Secret Resolution Architecture

**Decision**: Implement `ISecretResolver` with a strategy pattern supporting three backends: `EnvironmentSecretResolver`, `VaultSecretResolver`, `AwsSsmSecretResolver`. The resolver parses the `ref:<backend>:<path>` format and dispatches to the matching strategy.

**Rationale**: The constitution mandates `ref:env:`, `ref:vault:`, `ref:aws-ssm:` prefixes. A strategy pattern allows adding new backends (e.g., Azure Key Vault) without modifying existing resolver code. Resolution happens once at boot, and resolved values are cached in-memory for the process lifetime.

**Alternatives considered**:
- Single monolithic resolver class with switch/case — violates Open/Closed principle; harder to test individual backends.
- ASP.NET Configuration Providers (custom `IConfigurationSource`) — cleaner integration with `IOptions<T>` but requires building custom providers for each backend, and the `ref:*` prefix syntax doesn't map cleanly to the configuration provider model.

**Implementation notes**:
- `ISecretResolver` interface defined in Domain (zero dependencies).
- Concrete implementations live in Infrastructure.
- Resolution is implemented as a second `IStartupFilter` (`SecretResolutionFilter`) that runs before `VendorConfigValidationFilter`.
- Retry policy: exponential backoff with 3 attempts, 1s/2s/4s delays. After exhaustion, throw `SecretResolutionException` which crashes the container.
- Resolved secrets are stored in a `ResolvedSecretStore` (singleton, in-memory dictionary). Never logged, never serialized.

## R3: Runtime Configuration via DB-Backed VendorSettings

**Decision**: Runtime-tier configuration is persisted in a `VendorSettings` table in MSSQL. The Admin API patches individual sections. An in-memory `IOptionsMonitor<VendorRuntimeConfig>`-compatible cache is invalidated on write.

**Rationale**: DB-backed runtime config survives container restarts without requiring file-system state. `IOptionsMonitor<T>` provides change-notification semantics that downstream services can subscribe to. The Admin API mutation path validates the full config (including cross-section business rules) before persisting.

**Alternatives considered**:
- File-watcher on `vendor.config.json` — fragile in containerized environments; doesn't survive volume remounts; adds filesystem coupling.
- Redis pub/sub for config change propagation — unnecessary complexity for single-tenant single-instance deployments; constitution mandates IMemoryCache as default.
- Direct `IOptionsSnapshot<T>` with reloadable JSON — couples runtime mutability to file I/O; doesn't support Admin API as the mutation interface.

**Implementation notes**:
- `VendorSettings` entity stores JSON-serialized section blobs with a `Version` column for optimistic concurrency (ETag-based).
- `VendorSettingsRepository` implements `IVendorSettingsRepository` (Domain interface).
- `UpdateVendorSettingsCommand` handler validates the merged config (existing + patch), persists, invalidates cache, and raises `VendorSettingsUpdatedEvent` via outbox.
- Read path: `GetVendorConfigQuery` merges boot-time immutable config + DB runtime config into a unified `VendorConfigDto`.

## R4: CI/CD Validation Pipeline

**Decision**: Use `ajv-cli` for JSON Schema validation and a custom Node.js script (`scripts/audit-secrets.js`) for secret-reference auditing, both executed as GitHub Actions steps.

**Rationale**: `ajv-cli` is the industry-standard CLI for JSON Schema validation, supports Draft 2020-12, and provides human-readable error output. A custom secret-audit script gives precise control over which fields are classified as "secret" (via a `secretFields` manifest) and the `ref:*` pattern enforcement.

**Alternatives considered**:
- .NET-based CI validator (custom console app) — heavier build dependency; requires .NET SDK in CI runner; slower feedback loop.
- Pre-commit hooks only — too easy to bypass; CI enforcement is non-negotiable.
- Third-party secret scanning tools (GitGuardian, truffleHog) — detect committed secrets in git history but don't validate the `ref:*` structural requirement in config files.

**Implementation notes**:
- JSON Schema file: `config/vendor.config.schema.json` (Draft 2020-12).
- GitHub Actions workflow step 1: `npx ajv-cli validate -s config/vendor.config.schema.json -d config/vendor.config.json`.
- GitHub Actions workflow step 2: `node scripts/audit-secrets.js config/vendor.config.json` — exits with code 1 if any field in the `secretFields` list doesn't match `^ref:(env|vault|aws-ssm):.+$`.
- The `secretFields` list is maintained in `scripts/secret-fields.json` as an array of JSON paths (e.g., `$.payments[*].credentials.secretKey`).

## R5: Configuration Model Design — Three-Tier C# Type Hierarchy

**Decision**: Model the vendor configuration as three separate C# record types reflecting the tier boundaries: `VendorBuildConfig` (immutable after deploy), `VendorBootConfig` (immutable after startup), `VendorRuntimeConfig` (mutable via Admin API). A composite `VendorConfig` aggregate root in the Domain layer composes all three.

**Rationale**: Separating config types by mutability tier enforces tier discipline at the type level. The Admin API's `PATCH` endpoint accepts only `VendorRuntimeConfig` properties, making it impossible for callers to accidentally modify boot-time or build-time settings through a type mismatch.

**Alternatives considered**:
- Single flat `VendorConfig` class with runtime annotations — doesn't enforce tier immutability at compile time; relies on runtime attribute checks.
- Separate `appsettings.*.json` files per tier — splits configuration across multiple files; harder for vendors to understand the single `vendor.config.json` model.

**Implementation notes**:
- `VendorBuildConfig`: `VendorId` (string). Bound from `vendor.config.json` at deploy time. Registered as singleton.
- `VendorBootConfig`: `AuthConfig`, `CachingConfig`, `EmailConfig`, `AnalyticsConfig`, secret references. Bound from `vendor.config.json` after secret resolution. Registered as singleton.
- `VendorRuntimeConfig`: `BrandingConfig`, `LocaleConfig`, `TaxConfig`, `CheckoutConfig`, `PaymentProviderConfig[]`, `ShippingProviderConfig[]`, `PromotionsConfig`, `FeatureFlags`. Initially loaded from `vendor.config.json`, then overridden by DB-backed `VendorSettings` values.
- Value objects (`BrandingConfig`, `LocaleConfig`, `CheckoutConfig`, etc.) are defined in Domain with zero NuGet dependencies.
- EF Core owned types map `VendorSettings` value objects to columns on the `VendorSettings` table (no separate tables per Constitution Principle III).
