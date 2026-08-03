# Task 2 Report: GitHub Actions CI/CD Pipeline Setup

## Status: Completed

### Implementation Details
- Created `.github/workflows/ci-cd.yml` with the full multi-stage CI/CD pipeline.
- Workflow triggers on `push` and `pull_request` to `main` branch.
- Jobs included in pipeline:
  1. `lint-dockerfile`: Hadolint Dockerfile linting (`hadolint/hadolint-action@v3.1.0`).
  2. `build-and-test`: .NET 9 SDK restore, build, and test (`dotnet test Vendor.slnx`).
  3. `docker-build-push`: Docker Buildx setup, metadata extraction, GHCR login & image push.
  4. `deploy-staging`: Staging environment deployment step (runs on `push` to `main`).
  5. `deploy-production`: Production environment deployment step (runs after staging deployment on `push` to `main`).

### Git Commit
- Committed workflow file:
  `ci: add GitHub Actions CI/CD pipeline with hadolint, dotnet test, GHCR push, and environments`
