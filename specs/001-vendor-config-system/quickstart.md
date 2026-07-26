# Quickstart Validation Guide: Vendor Configuration System

**Feature**: 001-vendor-config-system
**Date**: 2026-07-25

## Prerequisites

- .NET 9 SDK installed
- MSSQL Server (LocalDB or Docker: `mcr.microsoft.com/mssql/server:2022-latest`)
- Node.js 20+ (for CI validation scripts)
- Docker (optional, for containerized testing)

## Setup

### 1. Start MSSQL (Docker)

```bash
docker run -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=YourStr0ng!Pass' \
  -p 1433:1433 --name mssql-dev \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Set Environment Variables (Secret References)

```bash
# Required for boot-time secret resolution (ref:env:* references)
export STRIPE_SECRET_KEY="sk_test_abc123"
export STRIPE_WEBHOOK_SECRET="whsec_test_xyz"
export JWT_SECRET="your-256-bit-secret-key-here-min-32-chars"
export SENDGRID_API_KEY="SG.test_key_here"
```

### 3. Prepare vendor.config.json

Ensure `config/vendor.config.json` exists with valid configuration. See `contracts/admin-config-api.md` for the full schema reference.

### 4. Apply Database Migrations

```bash
cd src/Vendor.Api
dotnet ef database update --project ../Vendor.Infrastructure
```

## Validation Scenarios

### Scenario 1: Successful Boot with Valid Configuration

**What it proves**: Three-tier config resolution, secret resolution, and FluentValidation boot gate all work end-to-end.

```bash
# Start the API
cd src/Vendor.Api
dotnet run

# Expected: Application starts successfully
# Console output includes:
#   info: VendorConfigValidationFilter[0] Vendor configuration validated successfully for vendor: acme-store
#   info: SecretResolutionFilter[0] All secret references resolved (4 secrets)
```

**Verify**: `curl http://localhost:5000/health` returns `200 OK`.

### Scenario 2: Boot Failure — Invalid Business Rules

**What it proves**: FluentValidation IStartupFilter crashes the container on business rule violations.

```bash
# Edit config/vendor.config.json:
# Set TWO payment providers with "isDefault": true

cd src/Vendor.Api
dotnet run

# Expected: Application fails to start
# Console output includes:
#   fail: VendorConfigValidationFilter[0] Vendor configuration validation FAILED
#   fail: VendorConfigValidationFilter[0] - Payments: Exactly one payment provider must be marked as default
#   Application shutting down...
# Exit code: non-zero
```

### Scenario 3: Boot Failure — Unresolvable Secret

**What it proves**: Missing secret reference crashes the container with descriptive error.

```bash
# Unset a required env var
unset STRIPE_SECRET_KEY

cd src/Vendor.Api
dotnet run

# Expected: Application fails to start
# Console output includes:
#   fail: SecretResolutionFilter[0] Failed to resolve secret reference: ref:env:STRIPE_SECRET_KEY
#   fail: SecretResolutionFilter[0] Environment variable 'STRIPE_SECRET_KEY' not found
#   Application shutting down...
# Exit code: non-zero
```

### Scenario 4: Runtime Config Update via Admin API

**What it proves**: Admin API can patch runtime-tier config without restart.

```bash
# 1. Get current config
curl -H "Authorization: Bearer <admin-token>" \
  http://localhost:5000/api/v1/admin/config

# 2. Patch runtime config (change primary color and toggle a feature flag)
curl -X PATCH \
  -H "Authorization: Bearer <admin-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "runtime": {
      "branding": { "primaryColor": "#DC2626" },
      "featureFlags": { "enableWishlist": true }
    },
    "version": 1
  }' \
  http://localhost:5000/api/v1/admin/config

# Expected: 200 OK with updated full config
# primaryColor now "#DC2626", enableWishlist now true

# 3. Verify the change persists without restart
curl -H "Authorization: Bearer <admin-token>" \
  http://localhost:5000/api/v1/admin/config | jq '.tiers.runtime.branding.primaryColor'
# Expected: "#DC2626"
```

### Scenario 5: Runtime Config — Tier Immutability Guard

**What it proves**: Admin API rejects modifications to build-time and boot-time fields.

```bash
# Try to modify boot-time caching provider
curl -X PATCH \
  -H "Authorization: Bearer <admin-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "boot": { "caching": { "provider": "Redis" } },
    "version": 2
  }' \
  http://localhost:5000/api/v1/admin/config

# Expected: 400 Bad Request
# { "errors": [{ "field": "boot.caching.provider", "message": "Boot-time configuration is immutable at runtime" }] }
```

### Scenario 6: CI Validation — JSON Schema + Secret Audit

**What it proves**: CI pipeline catches invalid configs and raw secrets before deployment.

```bash
# Schema validation (valid config)
npx ajv-cli validate -s config/vendor.config.schema.json -d config/vendor.config.json --spec=draft2020
# Expected: exit code 0

# Secret audit (valid config with ref:* references)
node scripts/audit-secrets.js config/vendor.config.json
# Expected: exit code 0

# Schema validation (invalid config — remove required field)
# Remove "vendorId" from vendor.config.json
npx ajv-cli validate -s config/vendor.config.schema.json -d config/vendor.config.json --spec=draft2020
# Expected: exit code 1 with error pointing to missing vendorId

# Secret audit (raw secret committed)
# Replace "ref:env:STRIPE_SECRET_KEY" with "sk_live_actual_key"
node scripts/audit-secrets.js config/vendor.config.json
# Expected: exit code 1, output lists the violation
```

## Test Commands

```bash
# Run all tests
dotnet test Vendor.sln

# Run domain tests only (pure unit tests, no infra)
dotnet test tests/Vendor.Domain.Tests

# Run API integration tests (requires MSSQL)
dotnet test tests/Vendor.Api.Tests
```

## Success Criteria Verification

| Criteria | How to Verify |
|----------|---------------|
| SC-001: Startup < 2s | Time `dotnet run` from launch to first health check response |
| SC-002: Invalid config fails < 500ms | Time `dotnet run` with invalid config to exit |
| SC-003: 0% raw secrets pass CI | Run secret audit against config with raw values |
| SC-004: Runtime update < 100ms | Measure PATCH response time via curl timing |
| SC-005: Zero code changes for new vendor | Clone repo, edit only config + theme, run successfully |
