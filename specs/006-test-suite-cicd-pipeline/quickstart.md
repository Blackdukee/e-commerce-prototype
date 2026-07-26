# Quickstart: Test Suite & CI/CD Pipeline

**Feature**: 006-test-suite-cicd-pipeline  
**Date**: 2026-07-25

This guide validates the feature end-to-end: running the full test suite locally, building the Docker image, and verifying the CI workflow contract.

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 9.0+ | Build and test |
| Docker Desktop | 24.x+ | Testcontainers (MSSQL) + local image build |
| Node.js | 20.x | `scripts/audit-secrets.js` |
| `npx` / `ajv-cli` | — | JSON schema validation |
| `hadolint` | latest | Dockerfile linting (optional locally) |

---

## Step 1 — Run the Full Test Suite

```bash
# From repository root
dotnet build Vendor.slnx --configuration Release

dotnet test Vendor.slnx \
  --no-build \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage \
  --logger "console;verbosity=normal"
```

**Expected outcome**: All four test projects report results. Coverage XML files are written to `./coverage/<test-project-guid>/coverage.cobertura.xml`.

> [!NOTE]
> `Vendor.Infrastructure.Tests` requires Docker Desktop running. The `MsSqlFixture` will pull `mcr.microsoft.com/mssql/server:2022-latest` on first run (~1.5 GB). Subsequent runs use the cached layer.

---

## Step 2 — Generate and Check Coverage Report

```bash
# Install ReportGenerator (once)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Merge and generate HTML report
reportgenerator \
  -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:"Html;Cobertura;MarkdownSummaryGithub" \
  -targetthresholds:"line:80"
```

**Expected outcome**: 
- `coverage/report/index.html` opens in browser showing per-project and overall line coverage.
- Overall line coverage ≥ 80%.
- Per-layer targets: Domain ≥90%, Application ≥85%, Infrastructure ≥70%, API ≥75%.
- Exit code 0 if thresholds met; exit code 1 if any threshold breached.

> [!TIP]
> Open `coverage/report/index.html` to see the full interactive coverage report with drill-down per class.

---

## Step 3 — Validate Vendor Configuration

```bash
# Install ajv-cli (once)
npm install -g ajv-cli ajv-formats

# JSON Schema validation
ajv validate \
  -s config/vendor.config.schema.json \
  -d config/vendor.config.json \
  -c ajv-formats \
  --spec=draft2020

# Secret reference audit
node scripts/audit-secrets.js config/vendor.config.json
```

**Expected outcome**:
- Schema validation exits with code 0 and prints `config/vendor.config.json valid`.
- Secret audit exits with code 0; code 1 with JSON-path error if any field contains a raw secret instead of a `ref:*` reference.

---

## Step 4 — Lint the Dockerfile

```bash
# Install hadolint (Windows)
winget install hadolint.hadolint

# Lint
hadolint Dockerfile
```

**Expected outcome**: Zero `error` or `warning` level lint violations. Acceptable rules can be ignored via `.hadolint.yaml` in the repo root.

---

## Step 5 — Build the Docker Image

```bash
# From repository root
docker build -t vendor-api:local .
```

**Expected outcome**: Multi-stage build completes. Final image size under ~300 MB (ASP.NET runtime base only).

---

## Step 6 — Run and Verify the Container

```bash
# Create minimal config mount directory
mkdir -p ./local-config ./local-theme
cp config/vendor.config.json ./local-config/vendor.config.json

# Run container
docker run --rm -d \
  -p 8080:8080 \
  -v "$(pwd)/local-config:/app/config:ro" \
  -v "$(pwd)/local-theme:/app/theme:ro" \
  --name vendor-api-test \
  vendor-api:local

# Wait for startup
sleep 3

# Verify health probe
curl -f http://localhost:8080/health/live
# Expected: 200 OK, body: {"status":"Healthy"}

# Verify non-root execution
docker exec vendor-api-test id
# Expected: uid=1654 (non-root APP_UID)

# Cleanup
docker stop vendor-api-test
```

---

## Step 7 — Verify CI/CD Pipeline Structure

After pushing a branch and opening a PR, the GitHub Actions dashboard should show:

| Job | Status |
|-----|--------|
| `validate` | ✅ Runs immediately on PR open |
| `build-test` | ✅ Runs after `validate` passes |
| `docker` | ⏭️ Skipped on PRs; runs on push to develop/main |
| `staging` | ⏭️ Skipped on PRs; runs on push to develop |
| `production` | ⏭️ Skipped on PRs; requires manual approval on push to main |

> [!IMPORTANT]
> The `production` environment must be configured with at least one required approver in **Repository Settings → Environments → production** for the manual approval gate to activate.

---

## Quick Reference: Test Layer Structure

See [data-model.md](../data-model.md) for the full test pyramid diagram and fixture type descriptions.  
See [contracts/contracts.md](../contracts/contracts.md) for package version contracts and Docker image interface.

---

## Validation Checklist

| Scenario | Command | Pass Condition |
|----------|---------|---------------|
| All tests pass | `dotnet test` | Exit code 0, 0 failures |
| Overall coverage ≥80% | `reportgenerator ... -targetthresholds:"line:80"` | Exit code 0 |
| Config schema valid | `ajv validate ...` | Exit code 0 |
| No raw secrets | `node scripts/audit-secrets.js ...` | Exit code 0 |
| Dockerfile clean | `hadolint Dockerfile` | 0 errors/warnings |
| Docker build succeeds | `docker build ...` | Exit code 0 |
| Health probe responds | `curl -f .../health/live` | HTTP 200 |
| Non-root process | `docker exec ... id` | Non-zero UID |
| Volume hot-swap | Edit mounted config, restart | New config loaded without image rebuild |
