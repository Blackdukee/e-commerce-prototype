# Task 1 Report: Multi-Stage Production Dockerfile

## Summary
Successfully created the root `Dockerfile` using the specified multi-stage definition for .NET 9 ASP.NET Core API application.

## Key Configurations
- **Build Stage**: Uses `mcr.microsoft.com/dotnet/sdk:9.0`, restores dependencies using layer caching (`Vendor.slnx` & `.csproj` files copied first), and publishes Release binary to `/app/publish`.
- **Runtime Stage**: Uses `mcr.microsoft.com/dotnet/aspnet:9.0`.
- **Security & User**: Configured with `USER app` for non-root execution.
- **Networking**: `ASPNETCORE_HTTP_PORTS=8080`, exposed port `8080`.
- **Storage Volumes**: `/app/config` and `/app/theme`.
- **Health Check**: Configured `HEALTHCHECK` pinging `http://localhost:8080/health/live` with curl.

## Git Commit
- Added `Dockerfile` and committed with message `feat(docker): add multi-stage production Dockerfile with non-root security context`.
