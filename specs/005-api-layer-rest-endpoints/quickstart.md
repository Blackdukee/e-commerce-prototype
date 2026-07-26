# Quickstart Validation Guide: API Layer

**Feature**: 005-api-layer-rest-endpoints  
**Date**: 2026-07-25

## Prerequisites

- .NET 9 SDK installed
- Feature 002 (Domain), 003 (Application), 004 (Infrastructure) implemented and tests passing
- MSSQL instance reachable (LocalDB or Testcontainers)
- Redis instance reachable (optional; `Memory` caching acceptable for dev)
- Seq running at `http://localhost:5341` (optional; logs fall back to console)

## Quick Start (Development)

```bash
# From repo root:
dotnet run --project src/Vendor.Api/Vendor.Api.csproj
# API available at: https://localhost:5001
# Swagger UI: https://localhost:5001/swagger
# Health live: https://localhost:5001/health/live
# Health ready: https://localhost:5001/health/ready
```

## Validation Scenarios

### 1. Pipeline Order Verification

**Goal**: Confirm all 9 middleware stages execute in the correct order.

```bash
# Request without X-Correlation-ID header
curl -v https://localhost:5001/api/v1/products
# Expected: X-Correlation-ID response header with generated GUID
# Expected: X-Content-Type-Options: nosniff in response headers
# Expected: Serilog log event with CorrelationId and RouteTemplate fields
```

### 2. API Versioning

```bash
curl https://localhost:5001/api/v1/products
# Expected: 200 OK with api-supported-versions: 1.0 response header

curl https://localhost:5001/api/v2/products
# Expected: 400 Bad Request with ProblemDetails indicating unsupported version
```

### 3. Rate Limiting

```bash
# Send 11 POST requests to /auth/login within 1 minute
for i in {1..11}; do curl -s -o /dev/null -w "%{http_code}\n" -X POST https://localhost:5001/api/v1/auth/login -H 'Content-Type: application/json' -d '{"email":"test@test.com","password":"wrong"}'; done
# Expected: First 10 = 401, 11th = 429 Too Many Requests
```

### 4. Maintenance Mode

```bash
# Enable maintenance mode via admin API
curl -X POST https://localhost:5001/api/v1/admin/settings/maintenance \
  -H 'Authorization: Bearer <admin_token>' \
  -H 'Content-Type: application/json' \
  -d '{"enabled": true}'

# Test non-admin route returns 503
curl -s -o /dev/null -w "%{http_code}\n" https://localhost:5001/api/v1/products
# Expected: 503 Service Unavailable

# Health checks remain accessible
curl -s -o /dev/null -w "%{http_code}\n" https://localhost:5001/health/live
# Expected: 200 OK
```

### 5. Auth Flow End-to-End

```bash
# Register
curl -X POST https://localhost:5001/api/v1/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"user@example.com","firstName":"Test","lastName":"User","password":"Password123!"}'
# Expected: 201 with AuthResponse containing AccessToken + RefreshToken

# Use token to access protected endpoint
curl https://localhost:5001/api/v1/customer/profile \
  -H 'Authorization: Bearer <access_token>'
# Expected: 200 CustomerDto
```

### 6. Webhook Validation

```bash
# Invalid Stripe signature → rejected
curl -X POST https://localhost:5001/api/v1/webhooks/stripe \
  -H 'Stripe-Signature: t=invalid,v1=badsig' \
  -H 'Content-Type: application/json' \
  -d '{"type":"payment_intent.succeeded"}'
# Expected: 400 Bad Request
```

### 7. Health Check Readiness

```bash
curl https://localhost:5001/health/ready
# Expected: 200 JSON with status:Healthy including mssql, redis, payments checks
# If Redis not running: 503 with degraded status
```

### 8. SignalR WebSocket Connection

```bash
# Using wscat or browser DevTools:
wscat -c 'wss://localhost:5001/hubs/admin?access_token=<admin_jwt>'
# Expected: WebSocket upgrade succeeds, connection established
```

### 9. ProblemDetails on Validation Failure

```bash
curl -X POST https://localhost:5001/api/v1/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"not-an-email","password":"short"}'
# Expected: 422 with ProblemDetails containing errors dictionary:
# { "Email": ["Invalid email format"], "Password": ["Must be at least 8 characters"] }
```

## Integration Test Run

```bash
dotnet test tests/Vendor.Api.Tests/Vendor.Api.Tests.csproj --logger "console;verbosity=normal"
# Expected: All tests pass, 75%+ line coverage
```

## Artifact References

- [Endpoint Registry](contracts/api-endpoint-registry.md) — All 63 endpoints with method, route, auth, request, and response shapes
- [Data Model](data-model.md) — Request/response DTOs and middleware state model
- [Research](research.md) — Technology decisions and implementation notes
