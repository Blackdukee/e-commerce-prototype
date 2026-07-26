# Contracts: Test Suite & CI/CD Pipeline

**Feature**: 006-test-suite-cicd-pipeline  
**Date**: 2026-07-25

---

This feature primarily produces internal test infrastructure and CI/CD configuration. The external-facing contracts are:

1. **GitHub Actions Workflow Interface** — input triggers, environment gate approvals, and output artifacts
2. **Docker Image Interface** — mount paths, exposed ports, health probe endpoints, image tags
3. **Coverage Report Interface** — published artifact format and threshold contract

---

## Contract 1: GitHub Actions CI/CD Workflow

**File**: `.github/workflows/ci-cd.yml`

### Inputs / Triggers

| Trigger | Condition | Jobs Activated |
|---------|-----------|---------------|
| `pull_request` | Any branch | `validate`, `build-test` |
| `push` to `develop` | After merge | `validate` → `build-test` → `docker` → `staging` |
| `push` to `main` | After merge | `validate` → `build-test` → `docker` → `production` |

### Environment Secrets Required

| Secret Name | Scope | Used By |
|-------------|-------|---------|
| `GITHUB_TOKEN` | Automatic | `docker` job (GHCR push) |
| `STAGING_DEPLOY_URL` | Repository secret | `staging` job |
| `PRODUCTION_DEPLOY_URL` | Repository secret | `production` job |

### Manual Approval Gate

- **Environment**: `production`  
- **Approvers**: Configured via GitHub Environment protection rules  
- **Trigger**: `push` to `main` after all prior jobs pass

### Output Artifacts

| Artifact Name | Contents | Retention |
|--------------|---------|-----------|
| `coverage-report` | HTML + Cobertura XML merged report | 30 days |
| Docker image in GHCR | `ghcr.io/<owner>/<repo>:<sha>` and `:latest` | Registry |

### Coverage Gate Contract

- **Tool**: `danielpalme/ReportGenerator-GitHub-Action@5`  
- **Input**: `coverage/**/coverage.cobertura.xml` (all four test projects)  
- **Threshold**: `-targetthresholds:line=80`  
- **Failure behavior**: Exit code 1, job fails, PR merge blocked

---

## Contract 2: Docker Image Interface

**File**: `Dockerfile` (repository root)

### Exposed Interface

| Property | Value |
|----------|-------|
| Base runtime image | `mcr.microsoft.com/dotnet/aspnet:9.0` |
| Exposed port | `8080` (HTTP) |
| Process user | `$APP_UID` (non-root, built into .NET 9 ASP.NET image) |
| Entrypoint | `dotnet Vendor.Api.dll` |

### Volume Mounts (Required for Operation)

| Container Path | Purpose | Required |
|---------------|---------|---------|
| `/app/config` | Vendor-specific `vendor.config.json` and resolved configuration | Yes |
| `/app/theme` | CSS variables, logos, email templates | Yes |

### Health Check Probe

| Property | Value |
|----------|-------|
| Endpoint | `GET http://localhost:8080/health/live` |
| Expected response | HTTP 200 OK |
| Interval | 30s |
| Timeout | 5s |
| Start period | 15s |
| Retries | 3 |

### Image Tags (GHCR)

| Tag Pattern | When Applied | Purpose |
|------------|-------------|---------|
| `ghcr.io/<owner>/<repo>:<git-sha>` | Every push to develop/main | Immutable deployment reference |
| `ghcr.io/<owner>/<repo>:latest` | Every push to develop/main | Convenience pointer |
| `ghcr.io/<owner>/<repo>:develop` | Push to develop | Branch pointer |
| `ghcr.io/<owner>/<repo>:main` | Push to main | Stable release pointer |

### Environment Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `ASPNETCORE_URLS` | Kestrel bind address | `http://+:8080` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | Set by deployment |

---

## Contract 3: Test Project Package Contracts

Each test project must reference these packages at minimum:

### Vendor.Domain.Tests

| Package | Version | Role |
|---------|---------|------|
| `xunit` | 2.9.2 | Test framework |
| `xunit.runner.visualstudio` | 2.8.2 | Test runner |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | Test host |
| `FluentAssertions` | 6.12.2 | Assertion library |
| `coverlet.collector` | 6.0.2 | Coverage collection |
| `Bogus` | 3.x | Domain test data |

### Vendor.Application.Tests

| Package | Version | Role |
|---------|---------|------|
| (all above) | — | — |
| `Moq` | 4.x | Repository interface mocking |

### Vendor.Infrastructure.Tests

| Package | Version | Role |
|---------|---------|------|
| `xunit`, `FluentAssertions`, `coverlet.collector`, `Bogus`, `Moq` | — | Base |
| `Testcontainers.MsSql` | 4.3.0 | Real MSSQL container fixture |
| `Respawn` | 6.2.1 | Database state reset between tests |

### Vendor.Api.Tests

| Package | Version | Role |
|---------|---------|------|
| `xunit`, `FluentAssertions`, `coverlet.collector`, `Bogus` | — | Base |
| `Microsoft.AspNetCore.Mvc.Testing` | 9.0.0 | WebApplicationFactory |
| `System.IdentityModel.Tokens.Jwt` | 8.x | JWT generation in AuthHelper |

---

## Contract 4: Smoke Test Endpoints (Staging/Production)

| Endpoint | Method | Expected Status | Used In |
|----------|--------|----------------|---------|
| `/health/ready` | GET | 200 OK | Staging smoke test |
| `/products` | GET | 200 OK | Staging smoke test |
| `/health/live` | GET | 200 OK | Production post-deploy health check |
