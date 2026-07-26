# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and csproj files for restore caching
COPY Vendor.slnx ./
COPY src/Vendor.Domain/Vendor.Domain.csproj src/Vendor.Domain/
COPY src/Vendor.Application/Vendor.Application.csproj src/Vendor.Application/
COPY src/Vendor.Infrastructure/Vendor.Infrastructure.csproj src/Vendor.Infrastructure/
COPY src/Vendor.Api/Vendor.Api.csproj src/Vendor.Api/
COPY tests/Vendor.Domain.Tests/Vendor.Domain.Tests.csproj tests/Vendor.Domain.Tests/
COPY tests/Vendor.Application.Tests/Vendor.Application.Tests.csproj tests/Vendor.Application.Tests/
COPY tests/Vendor.Infrastructure.Tests/Vendor.Infrastructure.Tests.csproj tests/Vendor.Infrastructure.Tests/
COPY tests/Vendor.Api.Tests/Vendor.Api.Tests.csproj tests/Vendor.Api.Tests/

RUN dotnet restore Vendor.slnx

# Copy all source files
COPY . .

# Build and Publish API
WORKDIR /src/src/Vendor.Api
RUN dotnet publish Vendor.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Final Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user and group
RUN groupadd -g 10001 appgroup && \
    useradd -u 10001 -g appgroup -s /bin/false appuser && \
    chown -R appuser:appgroup /app

USER appuser:appgroup

COPY --chown=appuser:appgroup --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
  CMD curl --fail http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Vendor.Api.dll"]
