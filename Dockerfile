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

USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Non-root user execution
USER app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

VOLUME ["/app/config", "/app/theme"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Vendor.Api.dll"]
