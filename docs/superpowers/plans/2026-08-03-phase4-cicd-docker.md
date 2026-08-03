# Phase 4 — CI/CD Pipeline & Production Dockerization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a Hadolint-compliant multi-stage production Dockerfile and automated GitHub Actions CI/CD pipeline for the .NET 9 e-commerce platform.

**Architecture:** A 2-stage Dockerfile (SDK 9.0 build + ASP.NET 9.0 non-root runtime) with non-privileged port 8080, health checks, and volume mounts, coupled with a 5-stage GitHub Actions workflow for linting, testing, container registry publishing, and environment deployments.

**Tech Stack:** .NET 9.0 SDK, ASP.NET Core 9.0 Runtime, Docker, Hadolint 3.1.0, GitHub Actions (`ghcr.io`, `docker/build-push-action@v5`).

## Global Constraints

- Docker base images MUST use .NET 9 (`mcr.microsoft.com/dotnet/sdk:9.0` and `mcr.microsoft.com/dotnet/aspnet:9.0`).
- Container process MUST run as non-root user `app`.
- HTTP port MUST be set to `8080` via `EXPOSE 8080` and `ENV ASPNETCORE_HTTP_PORTS=8080`.
- Dockerfile MUST contain volume declarations for `/app/config` and `/app/theme`.
- GitHub Actions MUST authenticate to `ghcr.io` using `${{ secrets.GITHUB_TOKEN }}`.

---

### Task 1: Hadolint-Compliant Multi-Stage Production `Dockerfile`

**Files:**
- Create: `Dockerfile`

**Interfaces:**
- Consumes: `src/Vendor.Api/Vendor.Api.csproj`, `Vendor.slnx`
- Produces: Production OCI container image running ASP.NET Core on port 8080 under user `app`

- [ ] **Step 1: Create the multi-stage Dockerfile**

```dockerfile
# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for layer caching
COPY ["src/Vendor.Api/Vendor.Api.csproj", "src/Vendor.Api/"]
COPY ["src/Vendor.Application/Vendor.Application.csproj", "src/Vendor.Application/"]
COPY ["src/Vendor.Domain/Vendor.Domain.csproj", "src/Vendor.Domain/"]
COPY ["src/Vendor.Infrastructure/Vendor.Infrastructure.csproj", "src/Vendor.Infrastructure/"]
COPY ["Vendor.slnx", "./"]

RUN dotnet restore "src/Vendor.Api/Vendor.Api.csproj"

# Copy remaining source code
COPY . .
WORKDIR "/src/src/Vendor.Api"
RUN dotnet publish "Vendor.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Non-root user execution
USER app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

VOLUME ["/app/config", "/app/theme"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Vendor.Api.dll"]
```

- [ ] **Step 2: Verify Dockerfile syntax**

Run: `docker build --no-cache -t vendor-api:test .`
Expected: Build finishes with `Successfully tagged vendor-api:test` or exits 0.

- [ ] **Step 3: Commit**

```bash
git add Dockerfile
git commit -m "feat(docker): add multi-stage production Dockerfile with non-root security context"
```

---

### Task 2: GitHub Actions Workflow (`.github/workflows/ci-cd.yml`)

**Files:**
- Create: `.github/workflows/ci-cd.yml`

**Interfaces:**
- Consumes: Repository PRs and `main` branch pushes
- Produces: Verified build, test results, container image pushed to `ghcr.io`, and environment deployments

- [ ] **Step 1: Create the GitHub Actions workflow file**

```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

jobs:
  lint-dockerfile:
    name: Lint Dockerfile
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Code
        uses: actions/checkout@v4

      - name: Hadolint Dockerfile Lint
        uses: hadolint/hadolint-action@v3.1.0
        with:
          dockerfile: Dockerfile

  build-and-test:
    name: Build & Test (.NET 9)
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Code
        uses: actions/checkout@v4

      - name: Setup .NET 9 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore Dependencies
        run: dotnet restore Vendor.slnx

      - name: Build Solution
        run: dotnet build Vendor.slnx --no-restore -c Release

      - name: Run Unit & Integration Tests
        run: dotnet test Vendor.slnx --no-build -c Release --logger "console;verbosity=normal"

  docker-build-push:
    name: Docker Build & Push (GHCR)
    needs: [lint-dockerfile, build-and-test]
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    steps:
      - name: Checkout Code
        uses: actions/checkout@v4

      - name: Log in to GitHub Container Registry
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Extract Docker Metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ghcr.io/${{ github.repository }}
          tags: |
            type=sha,prefix=sha-
            type=raw,value=latest,enable=${{ github.ref == 'refs/heads/main' }}

      - name: Build and Push Image
        uses: docker/build-push-action@v5
        with:
          context: .
          push: ${{ github.event_name != 'pull_request' }}
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}

  deploy-staging:
    name: Deploy to Staging
    needs: [docker-build-push]
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - name: Deploy to Staging Environment
        run: echo "Deploying container to Staging environment..."

  deploy-production:
    name: Deploy to Production
    needs: [deploy-staging]
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Deploy to Production Environment
        run: echo "Deploying container to Production environment..."
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/ci-cd.yml
git commit -m "ci: add GitHub Actions CI/CD pipeline with hadolint, dotnet test, GHCR push, and environments"
```

---

### Task 3: Solution Health & Verification Audit

**Files:**
- Test: All solution test projects

- [ ] **Step 1: Execute full clean test suite**

Run: `dotnet test Vendor.slnx --logger "console;verbosity=normal"`
Expected: All 234 tests pass cleanly.

- [ ] **Step 2: Verify git working directory**

Run: `git status`
Expected: Working tree clean.
