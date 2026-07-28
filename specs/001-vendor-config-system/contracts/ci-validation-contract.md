# CI Validation Contract: Vendor Configuration Pipeline

**Feature**: 001-vendor-config-system
**Date**: 2026-07-25

## Pipeline Overview

The CI validation pipeline runs on every push/PR that modifies `config/vendor.config.json` or `config/vendor.config.schema.json`.

## Step 1: JSON Schema Validation

**Tool**: `ajv-cli` (npm package)
**Command**: `npx -p ajv-cli -p ajv-formats ajv validate -c ajv-formats -s config/vendor.config.schema.json -d config/vendor.config.json --spec=draft2020`

**Exit codes**:
- `0`: Schema validation passes
- `1`: Schema validation fails (output includes JSON path + error message)

## Step 2: Secret Reference Audit

**Tool**: Custom script `scripts/audit-secrets.js`
**Command**: `node scripts/audit-secrets.js config/vendor.config.json`

**Input**: 
- `config/vendor.config.json` — the config file to audit
- `scripts/secret-fields.json` — manifest of JSON paths that MUST be secret references

**Secret fields manifest** (`scripts/secret-fields.json`):

```json
[
  "$.boot.auth.jwtSecret",
  "$.boot.auth.googleClientSecret",
  "$.boot.auth.facebookAppSecret",
  "$.boot.caching.redisConnectionString",
  "$.boot.email.sendGridApiKey",
  "$.boot.email.smtpPassword",
  "$.boot.analytics.forwardingSecret",
  "$.runtime.payments[*].credentials.secretKey",
  "$.runtime.payments[*].webhookSecret",
  "$.runtime.shipping[*].apiKey"
]
```

**Validation rules**:
1. For each JSON path in the manifest, if the field exists in the config file, its value MUST match `^ref:(env|vault|aws-ssm):.+$`.
2. If any matching field contains a value NOT matching the pattern, the audit fails.
3. Optional/nullable fields that are absent or null are skipped.

**Exit codes**:
- `0`: All secret fields use valid `ref:*` references
- `1`: One or more fields contain raw secret values (output lists violations)

**Output format** (on failure):
```
SECRET AUDIT FAILED:
  ✗ $.runtime.payments[0].credentials.secretKey = "sk_live_123..." (expected ref:* pattern)
  ✗ $.boot.email.sendGridApiKey = "SG.abc123..." (expected ref:* pattern)

2 violation(s) found. All secret fields must use ref:env:*, ref:vault:*, or ref:aws-ssm:* references.
```

## GitHub Actions Workflow Integration

```yaml
# .github/workflows/validate-vendor-config.yml
name: Validate Vendor Config
on:
  push:
    paths: ['config/vendor.config.json', 'config/vendor.config.schema.json']
  pull_request:
    paths: ['config/vendor.config.json', 'config/vendor.config.schema.json']

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - name: JSON Schema Validation
        run: npx -p ajv-cli -p ajv-formats ajv validate -c ajv-formats -s config/vendor.config.schema.json -d config/vendor.config.json --spec=draft2020
      - name: Secret Reference Audit
        run: node scripts/audit-secrets.js config/vendor.config.json
```
