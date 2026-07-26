# Implementation Plan: Test Suite & CI/CD Pipeline

**Branch**: `006-test-suite-cicd-pipeline` | **Date**: 2026-07-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-test-suite-cicd-pipeline/spec.md`

---

## Summary

Deliver a production-grade, four-layer test pyramid (Domain 90%, Application 85%, Infrastructure 70%, API 75%) backed by xUnit + FluentAssertions + Moq + Testcontainers (MSSQL) + Respawn + Bogus + WebApplicationFactory; a five-stage GitHub Actions CI/CD pipeline (validate → build-test → docker → staging → production); and a multi-stage .NET 9 Dockerfile with non-root execution, HEALTHCHECK, and volume-mounted vendor config/theme isolation.

---

## Technical Context

**Language/Version**: C# 13 / .NET 9.0

**Primary Dependencies**:
- Test: xUnit 2.9.2, FluentAssertions 6.12.2, Moq 4.x, Bogus 3.x
- Infrastructure tests: Testcontainers.MsSql 4.3.0, Respawn 6.2.1
- API tests: Microsoft.AspNetCore.Mvc.Testing 9.0.0, System.IdentityModel.Tokens.Jwt 8.x
- Coverage: coverlet.collector 6.0.2, ReportGenerator (GitHub Action)
- CI: GitHub Actions, hadolint, ajv-cli, Node.js 20

**Storage**: MSSQL (Testcontainers for integration tests — real SQL Server 2022 container)

**Testing**: xUnit — existing test projects already scaffolded; this feature fills their content

**Target Platform**: Linux container (ubuntu-latest for CI), Docker (mcr.microsoft.com/dotnet/aspnet:9.0 runtime)

**Performance Goals**: CI Stage 1 + Stage 2 total < 5 minutes; container cold-start to `/health/live` 200 OK < 3 seconds

**Constraints**: 
- Domain test project must have zero infrastructure NuGet dependencies (Constitution Principle I)
- No raw secrets in CI configuration or test fixtures (Constitution Principle V)
- Clone-per-vendor: Dockerfile VOLUME mounts; no vendor-specific code changes (Constitution Principle IV)

**Scale/Scope**: 4 test projects; ~40 domain tests, ~22 application tests, ~20 infrastructure tests, ~19 API tests (existing baseline); this feature expands to achieve stated coverage targets

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Compliance Status | Notes |
|-----------|------------------|-------|
| I. Clean Architecture — Strict Dependency Direction | ✅ PASS | `Vendor.Domain.Tests` has zero infrastructure NuGet refs. Application tests use Moq, not real infra. |
| II. Result-Oriented Handlers | ✅ PASS | Application handler tests verify `Result<T>` returns; no exception-based flow tested as success path. |
| III. MSSQL via EF Core — Owned Types | ✅ PASS | Infrastructure tests use real MSSQL via Testcontainers; owned type mappings verified through repository tests. |
| IV. Clone-Per-Vendor Isolation | ✅ PASS | Dockerfile declares `VOLUME ["/app/config", "/app/theme"]`; no vendor-specific code in test fixtures. |
| V. Secrets — Reference-Only Policy | ✅ PASS | `audit-secrets.js` in Stage 1 blocks PRs containing raw secrets; test JWTs use ephemeral test keys, not production secrets. |
| VI. Domain Events via Transactional Outbox | ✅ PASS | Infrastructure tests cover `OutboxProcessorHostedService` delivery; outbox message insertion verified in transaction tests. |
| VII. Test Coverage Targets | ✅ PASS | This feature is the implementation of Principle VII; coverage gate enforced at 80% overall in CI. |

---

## Project Structure

### Documentation (this feature)

```text
specs/006-test-suite-cicd-pipeline/
├── plan.md              # This file
├── research.md          # Phase 0 — testing patterns, CI tooling, Dockerfile best practices
├── data-model.md        # Phase 1 — test architecture, fixture types, CI job graph, Docker layers
├── quickstart.md        # Phase 1 — local validation guide
├── contracts/
│   └── contracts.md     # Phase 1 — workflow contract, Docker interface, package contracts
└── tasks.md             # Phase 2 output (/speckit-tasks command)
```

### Source Code (repository root)

```text
# Test Infrastructure
tests/
├── Vendor.Domain.Tests/
│   ├── Generators/               # Bogus Faker<T> factories for domain types
│   ├── Aggregates/               # Existing — aggregate invariant & state-machine tests
│   └── ValueObjects/             # Existing — value-object rule tests
│
├── Vendor.Application.Tests/
│   ├── Generators/               # Bogus fakers reused for handler inputs
│   ├── Handlers/                 # Existing — command/query handler tests (Moq)
│   ├── Validators/               # Existing — FluentValidation rule tests
│   └── Modules/                  # Pipeline behavior tests (ValidationBehavior, etc.)
│
├── Vendor.Infrastructure.Tests/
│   ├── Fixtures/                 # NEW — MsSqlFixture (Testcontainers + Respawn)
│   ├── Generators/               # Bogus fakers for infrastructure test data
│   ├── Persistence/              # Existing — repository tests (upgraded to Testcontainers)
│   ├── Outbox/                   # Existing — OutboxProcessor delivery tests
│   ├── Auth/                     # Existing — secret resolution tests
│   ├── Config/                   # Existing — VendorSettings repository tests
│   └── Payments/                 # Existing — payment adapter tests
│
└── Vendor.Api.Tests/
    ├── Helpers/                   # NEW — AuthHelper, VendorApiFactory
    ├── Generators/               # Bogus fakers for HTTP request bodies
    ├── Integration/               # Existing — endpoint integration tests (upgraded)
    └── Unit/                     # Existing — middleware unit tests

# CI/CD & Containerization
.github/
└── workflows/
    └── ci-cd.yml                  # NEW — 5-stage unified pipeline (replaces validate-vendor-config.yml)

Dockerfile                         # NEW — multi-stage .NET 9 build

.hadolint.yaml                     # NEW — hadolint suppress rules config
```

**Structure Decision**: All test additions land in the four existing test projects to match the established solution structure. No new test projects are created (Constitution prohibits unjustified complexity). Infrastructure shared between test layers (Fakers, Fixtures) lives in per-project `Generators/` and `Fixtures/` subfolders rather than a separate shared helper assembly, to avoid circular project references.

---

## Complexity Tracking

No Constitution violations introduced. All patterns (Testcontainers, Respawn, WebApplicationFactory, Bogus, GitHub Actions, multi-stage Dockerfile) are explicitly mandated or clearly implied by the spec and Constitution.

---

## Phase 0 Research Findings

See [research.md](./research.md) for full decision rationale. Key decisions:

| Topic | Decision |
|-------|---------|
| MSSQL integration tests | Testcontainers.MsSql 4.3.0 + Respawn 6.2.1 via `ICollectionFixture<MsSqlFixture>` |
| Coverage collection | `coverlet.collector` + `dotnet test --collect:"XPlat Code Coverage"` |
| Coverage gate | `danielpalme/ReportGenerator-GitHub-Action@5` with `-targetthresholds:line:80` |
| Auth in API tests | `AuthHelper` (System.IdentityModel.Tokens.Jwt) + `PostConfigure<JwtBearerOptions>` override |
| Test data | Bogus 3.x with static `Faker<T>` factories, seed `new Random(42)` |
| CI structure | Single `ci-cd.yml`, 5 jobs with `needs:` chain and `environment: production` gate |
| Dockerfile | SDK 9.0 build → ASP.NET 9.0 runtime, `USER $APP_UID`, port 8080, `/health/live` HEALTHCHECK |
| GHCR push | `docker/login-action@v3` + `GITHUB_TOKEN` + SHA tags via `docker/metadata-action@v5` |

---

## Phase 1 Design Artifacts

- [data-model.md](./data-model.md): Test pyramid architecture, fixture types, CI job graph, Docker layer model, package delta
- [contracts/contracts.md](./contracts/contracts.md): Workflow trigger/secret contracts, Docker image interface, smoke test endpoints, package version contracts
- [quickstart.md](./quickstart.md): 9-step end-to-end local validation guide with expected outcomes for each verification scenario
