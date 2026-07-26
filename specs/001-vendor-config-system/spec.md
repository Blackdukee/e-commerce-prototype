# Feature Specification: Vendor Configuration System

**Feature Branch**: `001-vendor-config-system`

**Created**: 2026-07-25

**Status**: Draft

**Input**: User description: "Build the vendor configuration system that lets a single codebase run as different vendor deployments without code changes. Configuration resolves in three tiers: build-time (vendorId, locked at deploy), boot-time (secrets, auth, caching, email provider selection validated once at startup and crashes the container on failure), and runtime (branding, checkout rules, feature flags, shipping/payment provider settings editable via an Admin API with no restart required). Config sections: vendorId, vendorDisplayName, branding (logo, colors, fonts, SEO meta), locale (language, currency, timezone, text direction), tax strategy, checkout behavior (guest checkout toggle, max items per order, order number prefix), an array of payment provider configs, an array of shipping provider configs, promotion engine settings, feature flags, analytics tracking + consent config, auth config (token expiry, OAuth client IDs, password policy), caching provider selection, and email provider selection. Secrets are never stored as plaintext every secret field must be a reference string in the form ref:env:VARIABLE, ref:vault:path, or ref:aws-ssm:/path, resolved at boot by a secret resolver. Config validation happens twice: at boot (fails fast, halts the container) and in CI (JSON Schema validation plus a secret-reference audit that fails the build if any raw secret value is committed). Key business rules to enforce: exactly one payment provider must be marked default, defaultCurrency must appear in supportedCurrencies, defaultLanguage must appear in supportedLanguages."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Three-Tier Vendor Configuration Resolution & Boot Validation (Priority: P1)

As a DevOps Engineer or Systems Administrator, I want the system to load and resolve vendor configuration across three strict tiers (build-time, boot-time, runtime) upon container startup and resolve all secret references so that vendor environments start up securely and crash immediately if misconfigured.

**Why this priority**: Core foundation of the clone-per-vendor architecture. The application cannot run safely without predictable multi-tier configuration resolution, boot-time validation, and secret resolution.

**Independent Test**: Can be tested by starting the application container with valid vs invalid `vendor.config.json` files and secret reference sources (env variables, vault, ssm), verifying that valid setups start successfully while invalid setups fail fast with exit code > 0 and clear error logs.

**Acceptance Scenarios**:

1. **Given** a valid `vendor.config.json` with resolved secret references (`ref:env:*`, `ref:vault:*`, `ref:aws-ssm:*`) and compliant business rules, **When** the application starts, **Then** boot-time configuration validation passes, secrets are resolved into memory, and the system registers the build-time `vendorId` as immutable.
2. **Given** a `vendor.config.json` where a secret field contains a raw plaintext secret instead of a valid reference format, **When** the application starts, **Then** boot-time validation fails, an explicit error log identifying the violation is generated, and the application process immediately halts (container crash).
3. **Given** a `vendor.config.json` violating business rules (e.g., zero or multiple default payment providers, or `defaultCurrency` not in `supportedCurrencies`), **When** the application boots, **Then** validation fails fast with descriptive violation details and prevents system startup.
4. **Given** an unresolvable secret reference (e.g. missing environment variable `ref:env:MISSING_KEY`), **When** the boot-time secret resolver runs, **Then** startup fails fast with a specific secret resolution exception.

---

### User Story 2 - Admin API for Dynamic Runtime Configuration Management (Priority: P2)

As a Vendor Administrator, I want to view and update runtime configuration settings (branding, checkout rules, feature flags, shipping/payment provider parameters) via an Admin API without restarting the application container or making code changes.

**Why this priority**: Enables vendors to operationalize day-to-day business adjustments (promotions, branding updates, provider parameters) instantly without downtime or deployment cycles.

**Independent Test**: Can be tested by invoking the Admin API endpoints to query existing configuration and patch runtime settings, then verifying that subsequent client requests immediately reflect updated runtime settings while build-time and boot-time settings remain unaffected and immutable.

**Acceptance Scenarios**:

1. **Given** an authenticated Admin user, **When** they send a `GET /api/v1/admin/config` request, **Then** the API returns the current full vendor configuration with sensitive secret reference paths obscured/redacted.
2. **Given** an authenticated Admin user, **When** they submit a patch to update runtime fields (e.g. modify primary branding color, update max items per order, toggle a feature flag), **Then** the update is validated against schema and business rules, applied in-memory, persisted to the runtime store, and active for immediate incoming requests without container restart.
3. **Given** an Admin user attempting to modify build-time (`vendorId`) or boot-time settings (caching provider selection, secret reference paths) via the API, **When** the update request is received, **Then** the API rejects the request with HTTP 400 Bad Request indicating that build-time and boot-time parameters are immutable at runtime.

---

### User Story 3 - CI/CD Schema & Secret-Reference Audit Pipeline (Priority: P3)

As a Security Engineer, I want CI/CD automated validation to run JSON Schema checks and a secret-reference audit against vendor config files prior to deployment so that invalid configurations or unencrypted committed secrets fail the build before reaching infrastructure.

**Why this priority**: Prevents bad configurations or raw credential leaks from ever being deployed to staging or production environments.

**Independent Test**: Can be tested by executing the standalone CI config validator script against a matrix of compliant, schema-invalid, and raw-secret-containing config files, verifying that compliant files pass and non-compliant files return non-zero exit codes.

**Acceptance Scenarios**:

1. **Given** a vendor repository containing a valid `vendor.config.json`, **When** the CI validation script runs, **Then** both JSON Schema validation and the secret-reference audit pass with exit code 0.
2. **Given** a pull request containing a `vendor.config.json` with a hardcoded raw API key (e.g., `sk_live_12345...`), **When** the CI secret-reference audit runs, **Then** the audit detects the non-conforming secret string and fails the build with a descriptive error.
3. **Given** a `vendor.config.json` with missing required schema fields or incorrect data types, **When** the CI JSON Schema validator runs, **Then** the build fails with exact line/field schema validation errors.

---

### Edge Cases

- What happens when a secret backend (e.g., Vault or AWS SSM) experiences a network glitch during boot? The secret resolver must implement a retry strategy with exponential backoff before failing fast and crashing the container.
- How does the system handle concurrent runtime configuration updates via the Admin API? The runtime config update must use optimistic concurrency control / version tagging to prevent race conditions and partial state overrides.
- What happens if an invalid runtime configuration patch is sent via Admin API? The change must be validated against the full configuration schema and business rules in memory before applying; if validation fails, the patch is rejected atomically without mutating current state.
- How are locale direction settings handled if an unsupported direction is passed? Validation must restrict `direction` strictly to `ltr` or `rtl`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a three-tier configuration resolution model:
  - **Build-Time**: `vendorId` (locked at deployment, completely immutable).
  - **Boot-Time**: Auth settings, caching provider selection, email provider selection, secret reference paths (validated once at application startup; startup fails and halts container if invalid).
  - **Runtime**: Branding, locale, tax strategy, checkout rules, payment/shipping provider parameters, promotion engine settings, feature flags, analytics tracking + consent (editable live via Admin API without restarting container).
- **FR-002**: System MUST structure vendor configuration into distinct, strongly typed sections:
  1. `vendorId` (string, unique identifier)
  2. `vendorDisplayName` (string)
  3. `branding` (logo URL, favicon, primary/secondary colors, typography/fonts, SEO metadata)
  4. `locale` (defaultLanguage, supportedLanguages, defaultCurrency, supportedCurrencies, timezone, text direction: `ltr` | `rtl`)
  5. `tax` (tax strategy, default tax rate, price display inclusion rules)
  6. `checkout` (guestCheckoutEnabled, maxItemsPerOrder, orderNumberPrefix)
  7. `payments` (array of payment provider configurations: provider name, enabled status, isDefault, credentials reference, supported methods)
  8. `shipping` (array of shipping provider configurations: provider name, enabled status, rate calculation method, settings)
  9. `promotions` (enabled status, coupon evaluation rules, stacking rules)
  10. `featureFlags` (dictionary of boolean feature toggles)
  11. `analytics` (tracking ID, provider name, cookie consent rules)
  12. `auth` (tokenLifetimeMinutes, refreshTokenLifetimeDays, OAuth client IDs, password policy rules)
  13. `caching` (provider: `Memory` | `Redis`, connection/key settings)
  14. `email` (provider: `SendGrid` | `SMTP`, sender address, template IDs)
- **FR-003**: System MUST enforce a secret reference policy where no raw secrets (API keys, private keys, connection credentials) are allowed in configuration files or code. Every secret field MUST be formatted as a reference string:
  - `ref:env:<VARIABLE_NAME>`
  - `ref:vault:<SECRET_PATH>`
  - `ref:aws-ssm:<PARAMETER_PATH>`
- **FR-004**: System MUST include a boot-time secret resolver that resolves all `ref:*` references into memory at application boot and provides decrypted values securely to application services without exposing them in logs or API responses.
- **FR-005**: System MUST perform boot-time fail-fast validation. If any schema rule, business rule, or secret resolution fails during startup, the application MUST log explicit error details and exit with a non-zero exit code (crashing the container).
- **FR-006**: System MUST enforce business rules during both boot-time and runtime validation:
  - Exactly **one** payment provider in the `payments` array MUST be marked `isDefault: true`.
  - `locale.defaultCurrency` MUST be present in `locale.supportedCurrencies`.
  - `locale.defaultLanguage` MUST be present in `locale.supportedLanguages`.
- **FR-007**: System MUST provide an Admin API endpoint (`GET /api/v1/admin/config`) to fetch current configuration with sensitive secret reference paths masked/redacted.
- **FR-008**: System MUST provide an Admin API endpoint (`PATCH /api/v1/admin/config`) allowing authorized administrators to update runtime tier configuration settings dynamically without application downtime or container restart.
- **FR-009**: System MUST reject any Admin API request that attempts to alter build-time (`vendorId`) or boot-time configuration tier parameters.
- **FR-010**: System MUST include a standalone CI validation tool/script that performs:
  1. JSON Schema validation of `vendor.config.json` files.
  2. A secret-reference audit that scans all string fields and fails the build if any field matching secret patterns contains a raw plaintext value instead of a valid `ref:*` prefix.

### Key Entities

- **VendorConfig**: Aggregate root encapsulating all 14 configuration sections for a single vendor deployment.
- **BrandingConfig**: Value object holding design system tokens (logo, colors, fonts, SEO tags).
- **LocaleConfig**: Value object holding language, currency, timezone, and layout direction rules (`ltr`/`rtl`).
- **CheckoutConfig**: Value object controlling guest checkout, item limits, and order ID formatting.
- **PaymentProviderConfig**: Entity within configuration array specifying gateway settings, credentials reference (`ref:*`), and default status (`isDefault`).
- **ShippingProviderConfig**: Entity within configuration array specifying provider settings and rate calculation rules.
- **AuthConfig**: Value object specifying token lifetimes, OAuth client IDs, and password policy rules.
- **SecretReference**: Value object encapsulating a secret pointer (`ref:env:*`, `ref:vault:*`, `ref:aws-ssm:*`) and its resolution logic.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Application startup completes in under 2 seconds when configuration is valid, including boot-time validation and secret resolution.
- **SC-002**: 100% of invalid configuration files (schema errors, missing secrets, business rule violations) fail boot validation and halt the application in under 500 milliseconds.
- **SC-003**: 0% raw secret values committed to configuration files pass CI secret-reference audit.
- **SC-004**: Runtime configuration updates via Admin API take effect across all client endpoints in under 100 milliseconds without application downtime or container restart.
- **SC-005**: 100% of vendor deployment operations (onboarding a new vendor) can be executed solely by creating `config/vendor.config.json` and `theme/` assets with zero C# or frontend code modifications.

## Assumptions

- Environment variables, HashiCorp Vault, or AWS SSM parameters referenced via `ref:*` exist and are accessible from the runtime container environment.
- The Admin API is protected by role-based authorization ensuring only users with `VendorAdmin` credentials can invoke runtime configuration endpoints.
- Single-instance vendor deployments use `IMemoryCache` for runtime config caching, with seamless transition to Redis when running multi-instance deployments.
