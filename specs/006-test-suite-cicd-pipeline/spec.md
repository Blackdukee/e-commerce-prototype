# Feature Specification: Test Suite & CI/CD Pipeline

**Feature Branch**: `006-test-suite-cicd-pipeline`  
**Created**: 2026-07-25  
**Status**: Draft  
**Input**: User description: "Build the test suite and CI/CD pipeline for the platform."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Layered Test Pyramid Suite (Priority: P1) 🎯 MVP

As a developer, I want a comprehensive, multi-layer test suite adhering to strict coverage targets across all 4 architecture layers (Domain 90%, Application 85%, Infrastructure 70%, API 75%) so that regressions are caught immediately and business invariants remain inviolable.

**Why this priority**: Comprehensive testing across all layers is the prerequisite for automated CI/CD gating and continuous delivery confidence.

**Independent Test**: Can be independently verified by executing `dotnet test` across all four test projects (`Vendor.Domain.Tests`, `Vendor.Application.Tests`, `Vendor.Infrastructure.Tests`, `Vendor.Api.Tests`) and validating that overall coverage meets or exceeds 80%.

**Acceptance Scenarios**:

1. **Given** pure domain aggregate and value object code, **When** executing `Vendor.Domain.Tests` with xUnit and FluentAssertions, **Then** all aggregate invariants, state transitions, and value-object rules pass with ≥90% line coverage and zero infrastructure dependencies.
2. **Given** CQRS command/query handlers and pipeline behaviors, **When** executing `Vendor.Application.Tests` with Moq mocked repositories and Bogus test data generators, **Then** handler logic and pipeline short-circuit behaviors pass with ≥85% line coverage.
3. **Given** infrastructure persistence and outbox implementations, **When** executing `Vendor.Infrastructure.Tests` with Testcontainers (real MSSQL container per test class) and Respawn database state reset, **Then** repository CRUD and outbox polling/delivery operate correctly with ≥70% line coverage.
4. **Given** ASP.NET Core Minimal API endpoints and middleware, **When** executing `Vendor.Api.Tests` using `WebApplicationFactory<Program>` and a shared `AuthHelper` generating admin/customer JWT tokens, **Then** full HTTP pipeline routes, security headers, rate limiting, and auth policies pass with ≥75% line coverage.

---

### User Story 2 - Automated PR Validation & Build/Test CI Pipeline (Priority: P1)

As a DevOps engineer, I want an automated GitHub Actions PR workflow that runs schema validation, secret reference audits, Dockerfile linting, test execution, and code coverage gating so that non-compliant or broken code cannot be merged.

**Why this priority**: Automated PR gating prevents secret leakage, invalid vendor configurations, broken builds, and coverage regressions from reaching shared branches.

**Independent Test**: Can be independently verified by opening a PR with valid vs. invalid configuration files / un-tested code, confirming the workflow blocks non-compliant commits and approves valid ones.

**Acceptance Scenarios**:

1. **Given** a Pull Request targeting `develop` or `main`, **When** Stage 1 (Validate) executes, **Then** `vendor.config.json` is validated against its JSON schema, `scripts/audit-secrets.js` verifies zero raw secrets exist, and `hadolint` lints the Dockerfile.
2. **Given** Stage 1 passes, **When** Stage 2 (Build & Test) executes, **Then** `dotnet build` compiles the solution, all four test projects run, code coverage report is generated, and the pipeline fails if overall coverage is under 80%.
3. **Given** Stage 2 passes, **When** Stage 3 (Docker) executes, **Then** a container image is built and pushed to GitHub Container Registry (GHCR).

---

### User Story 3 - Multi-Stage Containerization & Vendor Mount Isolation (Priority: P2)

As a system administrator, I want a production-ready multi-stage Dockerfile running as a non-root user with health checks and volume mounts for configuration and themes so that onboarding a new vendor never requires rebuilding the container image.

**Why this priority**: Non-root execution and volume-mounted configuration enforce clone-per-vendor isolation (Constitution Principle IV) and container security best practices.

**Independent Test**: Can be independently verified by building the Docker container, mounting external `/app/config` and `/app/theme` directories, launching the container, and confirming `/health/live` returns 200 OK.

**Acceptance Scenarios**:

1. **Given** the repository root, **When** `docker build` executes using the multi-stage Dockerfile, **Then** the .NET 9 SDK image handles restore/build/publish and the ASP.NET runtime image executes the compiled binary.
2. **Given** a running container instance, **When** inspected via container runtime commands, **Then** process executes under a non-root UID on port 8080 with an active HEALTHCHECK probing `http://localhost:8080/health/live`.
3. **Given** vendor configuration and theme assets stored in host directories, **When** mounted to `/app/config` and `/app/theme`, **Then** the application reads the mounted configuration without requiring image rebuilds or code changes.

---

### User Story 4 - Automated Staging & Production Deployment Pipelines (Priority: P2)

As a release manager, I want automated deployment pipelines for Staging (on merge to `develop`) and Production (on merge to `main` with manual approval) with post-deploy health checks so that releases are safe and reliable.

**Why this priority**: Continuous deployment to Staging speeds up verification, while manual approval and smoke tests safeguard Production releases.

**Independent Test**: Can be independently verified by triggering deployments to Staging and Production, verifying automatic smoke tests (`/health/ready`, `/products`) on Staging, and verifying manual approval gates on Production.

**Acceptance Scenarios**:

1. **Given** a pull request merged to `develop`, **When** Stage 4 (Staging) executes, **Then** application automatically deploys to Staging and runs automated smoke tests against `/health/ready` and `/products`.
2. **Given** a pull request merged to `main`, **When** Stage 5 (Production) executes, **Then** pipeline pauses for manual approval, deploys to Production upon approval, and executes a post-deploy health check.

---

### Edge Cases

- How does the Infrastructure test runner handle Docker daemon unavailability? Testcontainers tests skip gracefully or fail fast with descriptive diagnostic output if Docker daemon is unreachable.
- What happens if a secret reference audit finds a raw secret in a test fixture or config override? The audit script fails the pipeline instantly, outputting the exact JSON path violating `ref:*` policy.
- How are port conflicts handled during parallel test execution? Testcontainers dynamically binds free host ports for MSSQL container instances per test class.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Domain layer test suite MUST achieve ≥90% line coverage using pure xUnit and FluentAssertions with zero infrastructure or external package dependencies.
- **FR-002**: Application layer test suite MUST achieve ≥85% line coverage using xUnit, Moq for repository interfaces, and pipeline-behavior verification tests.
- **FR-003**: Infrastructure layer test suite MUST achieve ≥70% line coverage using xUnit, Testcontainers for real MSSQL instances per test class, and Respawn for database state resetting between test runs.
- **FR-004**: API layer test suite MUST achieve ≥75% line coverage using xUnit and `WebApplicationFactory<Program>`, testing endpoints, middleware, authentication, and rate limiting with a shared `AuthHelper` generating admin/customer JWTs.
- **FR-005**: All test suites MUST utilize Bogus generators for producing realistic domain test data.
- **FR-006**: CI pipeline Stage 1 MUST validate `vendor.config.json` against `vendor.config.schema.json`, run `audit-secrets.js` to fail on raw secrets, and lint the Dockerfile.
- **FR-007**: CI pipeline Stage 2 MUST run `dotnet build`, execute all four test projects, aggregate code coverage, and enforce an 80% overall coverage gate.
- **FR-008**: CI pipeline Stage 3 MUST build a multi-stage Docker image and push it to GHCR after Stage 2 passes.
- **FR-009**: CI pipeline Stage 4 MUST auto-deploy Staging on merge to `develop` and execute smoke tests against `/health/ready` and `/products`.
- **FR-010**: CI pipeline Stage 5 MUST enforce manual environment approval on merge to `main` before deploying to Production and running post-deploy health checks.
- **FR-011**: Dockerfile MUST use a multi-stage build (SDK image for build, ASP.NET 9 runtime image for execution), run as non-root user on port 8080, include a HEALTHCHECK against `/health/live`, and expose volume mounts for `/app/config` and `/app/theme`.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Overall test coverage across the solution meets or exceeds 80% (Domain ≥90%, Application ≥85%, Infrastructure ≥70%, API ≥75%).
- **SC-002**: CI PR validation workflow completes Stage 1 and Stage 2 in under 5 minutes.
- **SC-003**: 100% of raw secret leaks in config files are caught and blocked in Stage 1 before test execution.
- **SC-004**: Docker image cold start to `/health/live` returning 200 OK completes in under 3 seconds.
- **SC-005**: Zero code or image rebuilds required when updating mounted vendor configuration and theme assets.

---

## Assumptions

- Target CI platform is GitHub Actions (`.github/workflows/ci-cd.yml`).
- Testcontainers requires Docker daemon available on CI runner (`ubuntu-latest`).
- GHCR authentication utilizes standard `GITHUB_TOKEN` secrets provided by GitHub Actions runner context.
- Staging and Production deployment targets expose HTTP endpoints accessible by CI runner smoke test steps.
