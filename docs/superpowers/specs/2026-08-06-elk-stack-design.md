# ELK Stack (Elasticsearch & Kibana) Logging Integration Design

## 1. Overview & Objectives
Integrate Kibana and the Serilog Elasticsearch Sink (`Elastic.Serilog.Sinks`) into the `e-commerce-prototype` solution to enable centralized structured log aggregation, error tracking, and visual telemetry dashboards.

## 2. Infrastructure Setup (`docker-compose.infra.yml`)
- **Elasticsearch**: Already containerized (`vendor-elasticsearch`) running on port `9200`.
- **Kibana**: Add container `vendor-kibana` running on port `5601` configured to connect to `http://elasticsearch:9200`.

### Container Specification:
```yaml
  kibana:
    image: docker.elastic.co/kibana/kibana:8.13.4
    container_name: vendor-kibana
    restart: unless-stopped
    ports:
      - "5601:5601"
    environment:
      - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
    depends_on:
      elasticsearch:
        condition: service_healthy
```

## 3. Application Logging Integration (`Vendor.Api`)
- **Package**: `Elastic.Serilog.Sinks` added to `src/Vendor.Api/Vendor.Api.csproj`.
- **Log Index Pattern**: `vendor-api-logs-{yyyy.MM.dd}`
- **Enrichments**:
  - `CorrelationId` (via `CorrelationIdMiddleware`)
  - `Environment` (`ASPNETCORE_ENVIRONMENT`)
  - `MachineName` & `ThreadId`

### Configuration (`appsettings.json`):
```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Elastic.Serilog.Sinks" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Hangfire": "Information"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodes": [ "http://localhost:9200" ],
          "indexFormat": "vendor-api-logs-{0:yyyy.MM.dd}"
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  }
}
```

## 4. Verification & Testing
1. Execute `docker compose -f docker-compose.infra.yml up -d` to verify all 4 containers (`mssql`, `redis`, `elasticsearch`, `kibana`) start and pass health checks.
2. Launch `dotnet run --project src/Vendor.Api/Vendor.Api.csproj`.
3. Verify that logs are automatically indexed in Elasticsearch (`http://localhost:9200/vendor-api-logs-*`).
4. Access Kibana at `http://localhost:5601` and verify data view setup (`vendor-api-logs-*`).
5. Execute full test suite (`dotnet test Vendor.slnx`) to guarantee zero regression across unit/integration suites.
