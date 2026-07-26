# Research: Test Suite & CI/CD Pipeline

**Feature**: 006-test-suite-cicd-pipeline  
**Date**: 2026-07-25

---

## R1: Testcontainers + Respawn for MSSQL Integration Tests

**Decision**: Use `Testcontainers.MsSql` (v4.3.0) wrapped in an xUnit `ICollectionFixture<MsSqlFixture>` implementing `IAsyncLifetime` to start a single SQL Server 2022 container shared across integration test classes in the collection. Integrate `Respawn` (v6.2.1) using `Respawner.CreateAsync` to reset user tables between individual test executions, excluding `__EFMigrationsHistory`. Execute EF Core migrations once during `InitializeAsync`.

**Rationale**: Sharing a single SQL Server container across a collection minimizes startup overhead. Respawn provides sub-10ms database resetting by issuing targeted SQL clearing operations while dynamically handling foreign key constraints. This delivers deterministic, fast, isolated integration tests against a real SQL Server engine.

**Alternatives considered**:
- EF Core In-Memory / SQLite — misses SQL Server-specific dialect features, raw SQL, and transaction isolation.
- Drop-and-re-migrate per test — too slow; unacceptable for CI.
- `TransactionScope` rollback — fails for async background workers (`OutboxProcessorHostedService`) that use separate connections.

**Implementation notes**:
- `MsSqlFixture : IAsyncLifetime` in `Vendor.Infrastructure.Tests` starts container, applies migrations, and creates `Respawner`.
- `[Collection("Database")]` attribute applied to all infrastructure integration test classes.
- `Respawner` configured with `TablesToIgnore = [new Table("__EFMigrationsHistory")]`.
- Connection string injected into `DbContextOptions<VendorDbContext>` via `DbContextOptionsBuilder`.
- Missing packages to add to `Vendor.Infrastructure.Tests.csproj`: `Testcontainers.MsSql`, `Respawn` (replacing the existing `Microsoft.EntityFrameworkCore.InMemory` and `NSubstitute`).

---

## R2: coverlet + ReportGenerator for Coverage Gating in GitHub Actions

**Decision**: Configure all four test projects with `coverlet.collector` (v6.0.2) and execute `dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage`. Use `danielpalme/ReportGenerator-GitHub-Action@5` to merge per-project Cobertura XML into a single consolidated report and enforce `line:80` threshold, failing the build if coverage falls below 80%.

**Rationale**: `coverlet.collector` operates cross-platform without locking issues. Merging into a single consolidated report ensures overall solution health gating rather than siloed per-project evaluation. ReportGenerator produces rich PR Markdown summaries and removes the need for custom parsing scripts.

**Alternatives considered**:
- `coverlet.msbuild` (`/p:CollectCoverage=true`) — file locking issues during parallel multi-project runs.
- Codecov / Coveralls — external network dependency, subscription costs, API token management.
- Custom PowerShell XML parsing — brittle when schemas or project structures evolve.

**Implementation notes**:
- All four test projects already have `coverlet.collector` v6.0.2 ✅
- GitHub Actions workflow step: `dotnet test --no-build --collect:"XPlat Code Coverage" --results-directory ./coverage`
- ReportGenerator step merges files matching `coverage/**/coverage.cobertura.xml`
- Threshold enforced: `-targetthresholds:line=80`
- Output: HTML report archived as `coverage-report` artifact; summary posted to PR as job summary.

---

## R3: WebApplicationFactory JWT Auth Testing Pattern

**Decision**: Implement a shared `AuthHelper` in `Vendor.Api.Tests` using `System.IdentityModel.Tokens.Jwt` to generate signed HS256 JWTs for Admin and Customer roles against a fixed test secret key. Customize `WebApplicationFactory<Program>` via `WithWebHostBuilder` + `PostConfigure<JwtBearerOptions>` to override `TokenValidationParameters` with the test key, issuer, and audience. Expose `client.WithAdminBearerToken()` / `client.WithCustomerBearerToken()` extension methods.

**Rationale**: Real signed JWTs exercise the full authentication middleware pipeline, claim transformation, and role authorization policies. Overriding `JwtBearerOptions` isolates tests from environment secrets while enabling fast offline execution. Extension methods standardize auth setup and reduce duplication across test classes.

**Alternatives considered**:
- `TestAuthHandler` that auto-authenticates all requests — hides configuration bugs in real middleware policies.
- Mock identity server (Keycloak / Duende) — too slow; increases container management complexity.
- Unsigned or arbitrary token strings — fails when signature validation is active.

**Implementation notes**:
- `AuthHelper` located at `tests/Vendor.Api.Tests/Helpers/AuthHelper.cs`.
- `VendorApiFactory : WebApplicationFactory<Program>` overrides `JwtBearerOptions.TokenValidationParameters`.
- Test JWT secret: `"vendor-test-signing-key-256-bits!!"` (32+ chars for HS256).
- Admin JWT claims: `role=Admin`, `sub=admin-user-id`.
- Customer JWT claims: `role=Customer`, `sub=customer-user-id`.
- Existing `Vendor.Api.Tests.csproj` already has `Microsoft.AspNetCore.Mvc.Testing` ✅; add `System.IdentityModel.Tokens.Jwt` package.

---

## R4: Bogus (Faker) for Realistic Domain Test Data

**Decision**: Define static `Faker<T>` factory classes for domain aggregates (`CustomerFaker`, `OrderFaker`, `ProductFaker`, `CartFaker`) in a shared `Vendor.TestHelpers` namespace. Use `.CustomInstantiator()` to invoke domain factory methods/constructors. Set `Randomizer.Seed = new Random(42)` in a global `[AssemblyFixture]` or static initializer for deterministic runs. Expose fluent override methods for test-specific attribute variations.

**Rationale**: Centralized generators prevent duplicate test setup code and stay resilient to domain refactoring. `.CustomInstantiator()` respects DDD encapsulation by enforcing invariant validation. Fixed seed ensures 100% reproducible data across local and CI environments.

**Alternatives considered**:
- Hardcoded test data per test — verbose, fragile on schema changes.
- AutoFixture — complex customization required for private constructors and DDD encapsulation rules.
- Un-seeded Bogus — non-deterministic; intermittent CI failures on randomized edge cases.

**Implementation notes**:
- All test projects reference a shared `Vendor.TestHelpers` project (to be created as a test-support helper project, or alternatively inline `Generators/` folders per test project).
- Recommended approach: inline `Generators/` subfolder in each test project to avoid circular project references.
- `Bogus` package version: latest stable (3.x).
- Fakers wrap domain constructors; for aggregates with factory methods (e.g., `Order`), `.CustomInstantiator()` calls the constructor directly using valid parameter combinations.

---

## R5: GitHub Actions Multi-Stage Workflow Structure

**Decision**: Single workflow file `.github/workflows/ci-cd.yml` triggered on `pull_request` (any branch) and `push` to `develop`/`main`. Five sequential jobs using `needs:` chain: `validate` → `build-test` → `docker` (push only on develop/main) → `staging` (push to develop) → `production` (push to main, requires `environment: production` manual approval).

**Rationale**: Single-file pipeline provides clear visual DAG tracking in GitHub Actions. `needs:` dependencies guarantee early failures halt downstream execution. Conditional `if:` expressions skip deployment stages on PRs. Native GitHub Environments enforce manual approval for production without custom scripting.

**Alternatives considered**:
- Multiple workflow files per trigger/branch — high maintenance burden, configuration duplication.
- External approval webhooks — less secure, more infrastructure to maintain.
- `workflow_dispatch` only for production — loses automation chain, requires manual trigger.

**Implementation notes**:
- `validate` job: JSON Schema validation (`ajv-cli`), secret audit (`node scripts/audit-secrets.js`), Dockerfile lint (`hadolint/hadolint-action@v3.1.0`).
- `build-test` job: `dotnet build`, `dotnet test --collect:"XPlat Code Coverage"`, ReportGenerator coverage gate.
- `docker` job: `docker/login-action@v3` with `GITHUB_TOKEN`, `docker/metadata-action@v5` for SHA + latest tags, `docker/build-push-action@v5` with `cache-from/to: type=gha`.
- `staging` job: deploys on push to develop; smoke tests against `/health/ready` and `/products`.
- `production` job: `environment: production` for manual approval, deploys on merge to main, post-deploy health check.
- Existing `validate-vendor-config.yml` will be superseded by the new unified `ci-cd.yml` workflow.

---

## R6: Multi-Stage Dockerfile for .NET 9

**Decision**: Two-stage Dockerfile: `mcr.microsoft.com/dotnet/sdk:9.0` for restore/build/publish, `mcr.microsoft.com/dotnet/aspnet:9.0` for runtime. Use `USER $APP_UID` (built-in non-root user), `EXPOSE 8080`, `HEALTHCHECK` against `http://localhost:8080/health/live`, and `VOLUME ["/app/config", "/app/theme"]`.

**Rationale**: Multi-stage build minimizes attack surface by excluding SDK tools from runtime image. .NET 9 ASP.NET runtime images provide a built-in `$APP_UID` non-root user and default to port 8080. HEALTHCHECK enables orchestrator liveness probing. VOLUME declarations ensure vendor config/theme hot-swappability without image rebuilds.

**Alternatives considered**:
- Single-stage SDK image for runtime — multi-gigabyte images containing build tools, high attack surface.
- Running as root on port 80 — container escape risk, violates least-privilege principles.
- Hardcoded configuration inside image — breaks clone-per-vendor isolation (Constitution Principle IV).

**Implementation notes**:
- Dockerfile located at repository root.
- Build stage: `dotnet restore`, `dotnet build --no-restore`, `dotnet publish --no-build -o /app/publish`.
- Runtime stage: `COPY --from=build /app/publish .`, `USER $APP_UID`, `ENTRYPOINT ["dotnet", "Vendor.Api.dll"]`.
- `HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD curl -f http://localhost:8080/health/live || exit 1`.
- `ENV ASPNETCORE_URLS=http://+:8080` set in runtime stage.

---

## R7: GHCR Image Push

**Decision**: Authenticate to `ghcr.io` via `docker/login-action@v3` using `secrets.GITHUB_TOKEN` with `packages: write`. Generate SHA + `latest` tags using `docker/metadata-action@v5`. Build and push using `docker/build-push-action@v5` with `cache-from/to: type=gha`.

**Rationale**: `GITHUB_TOKEN` provides zero-overhead, scope-limited authentication without long-lived PAT secrets. SHA tags ensure deployment immutability and precise traceability. GitHub Actions BuildKit layer caching via `type=gha` accelerates subsequent workflow runs significantly.

**Alternatives considered**:
- PAT tokens — secret sprawl, rotation burden.
- Tag as `latest` only — deployment ambiguity, rollback difficulty.
- Raw `docker` CLI — loses native BuildKit caching and structured metadata.
