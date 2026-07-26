# Research: API Layer Composition Root & REST Endpoints

**Feature**: 005-api-layer-rest-endpoints
**Date**: 2026-07-25

## R1: Minimal API Endpoint Grouping with RouteGroupBuilder

**Decision**: Use `MapGroup()` with module extension methods — one file per functional area (`AuthEndpoints.cs`, `ProductEndpoints.cs`, etc.) in `src/Vendor.Api/Endpoints/`.

**Rationale**: `MapGroup()` allows prefix inheritance, shared metadata (auth, rate limiting, versioning), and shared filters without repetition. Extension methods on `IEndpointRouteBuilder` keep `Program.cs` clean and allow each module to be tested or evolved independently.

**Alternatives Considered**:
- Flat registration in `Program.cs`: works for small APIs but becomes unmaintainable at 63+ endpoints.
- Carter library: third-party dependency; native `MapGroup` + extension methods achieve the same goal with zero added packages.
- Custom `IEndpointDefinition` abstraction: adds indirection; `MapGroup` is idiomatic and sufficient.

**Implementation Notes**:
- `Program.cs` calls `app.MapAllEndpoints(versionSet)` which chains all module registration methods.
- Each module class: `public static class AuthEndpoints { public static IEndpointRouteBuilder MapAuthEndpoints(this RouteGroupBuilder group) { ... } }`
- Shared metadata applied at group level: `.RequireAuthorization()`, `.WithTags("Auth")`, `.RequireRateLimiting("auth")`.

## R2: Asp.Versioning.Http URL-Segment API Versioning

**Decision**: `Asp.Versioning.Http` v8.x with `UrlSegmentApiVersionReader` and route template `/api/v{version:apiVersion}/`.

**Rationale**: Official successor to the deprecated `Microsoft.AspNetCore.Mvc.Versioning`. First-class Minimal API support via `WithApiVersionSet()` and `MapToApiVersion()`. URL-segment versioning is CDN-cacheable and discoverable.

**Alternatives Considered**:
- Query string versioning (`?api-version=1.0`): harder to cache at CDN level.
- Header versioning: requires clients to send custom headers; not discoverable.
- Hardcoded `/api/v1/` prefix: loses deprecation warnings, sunset headers, and future v2 migration path.

**Implementation Notes**:
```csharp
builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(1, 0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ReportApiVersions = true;
    opt.ApiVersionReader = new UrlSegmentApiVersionReader();
});
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();
var v1 = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);
```

## R3: Built-in Rate Limiting — 4 Named Fixed-Window Policies

**Decision**: `Microsoft.AspNetCore.RateLimiting` (built-in .NET 7+) with `AddFixedWindowLimiter` for all 4 policies: `auth` (10/min), `catalog` (300/min), `webhook` (50/min), `default` (100/min).

**Rationale**: No external package required. Fixed-window is appropriate for per-minute thresholds with predictable reset semantics. `RequireRateLimiting("name")` applied per endpoint group keeps configuration co-located with endpoint definitions.

**Alternatives Considered**:
- Sliding window: more accurate burst control but higher memory overhead; not warranted by defined thresholds.
- Token bucket: smooth traffic shaping; overkill for this use case.
- `AspNetCoreRateLimit` package: not needed since built-in covers all requirements.

**Implementation Notes**:
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth",    o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 10; });
    options.AddFixedWindowLimiter("catalog", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 300; });
    options.AddFixedWindowLimiter("webhook", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 50; });
    options.AddFixedWindowLimiter("default", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 100; });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
// Middleware position: before UseAuthentication
app.UseRateLimiter();
```

## R4: Serilog.AspNetCore Structured Request Logging

**Decision**: `Serilog.AspNetCore` with `UseSerilogRequestLogging()` enriched with correlation ID and route template. Console + Seq sinks. Seq URL read from config.

**Rationale**: `UseSerilogRequestLogging()` replaces ASP.NET Core's default request logging and integrates with the Serilog pipeline for structured Console + Seq output. `Serilog.Enrichers.CorrelationId` automatically attaches `X-Correlation-ID` to all log events.

**Alternatives Considered**:
- `Microsoft.Extensions.Logging` custom middleware: less structured, no built-in Seq sink.
- OpenTelemetry: better for distributed tracing but significantly heavier setup.

**Implementation Notes**:
- Packages: `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.Seq`, `Serilog.Enrichers.CorrelationId`
- Bootstrap before `WebApplication.CreateBuilder()`: `Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext().Enrich.WithCorrelationId().WriteTo.Console().WriteTo.Seq(seqUrl).CreateLogger();`
- `builder.Host.UseSerilog();`
- `app.UseSerilogRequestLogging(opts => { opts.EnrichDiagnosticContext = EnrichFromRequest; });`

## R5: Global ProblemDetails Exception Handler

**Decision**: Custom `GlobalExceptionHandler : IExceptionHandler` registered via `AddExceptionHandler<GlobalExceptionHandler>()` + `app.UseExceptionHandler()`. `Result<T>` failures mapped via `ResultExtensions.ToHttpResult()`.

**Rationale**: `IExceptionHandler` (introduced .NET 8, available in .NET 9) is the idiomatic centralized exception handler for Minimal APIs. Keeping `Result<T>` mapping separate (in `ResultExtensions`) maintains the separation between domain failures and infrastructure exceptions.

**Alternatives Considered**:
- Custom middleware: more boilerplate; `IExceptionHandler` is the built-in, composable alternative.
- `UseStatusCodePages`: handles status codes only, not exception types.

**HTTP Status Code Mapping**:
| Exception / Result Error | HTTP Status |
|--------------------------|-------------|
| `ValidationException` | 422 Unprocessable Entity |
| `BusinessRuleViolationException` | 409 Conflict |
| `NotFoundException` | 404 Not Found |
| `UnauthorizedAccessException` | 401 Unauthorized |
| `ForbiddenException` | 403 Forbidden |
| Unhandled Infrastructure Exception | 500 Internal Server Error |

## R6: SignalR Hub JWT Authentication from Query String

**Decision**: `JwtBearerEvents.OnMessageReceived` extracts `access_token` from query string when path starts with `/hubs/admin`.

**Rationale**: WebSocket connections cannot send `Authorization` headers during the HTTP upgrade handshake. `OnMessageReceived` is the ASP.NET Core officially documented pattern for SignalR JWT auth.

**Alternatives Considered**:
- Cookie-based auth: requires cookie issuance; incompatible with JWT-primary strategy.
- Custom middleware to inject Authorization header: fragile; intercepts at wrong pipeline stage.

**Implementation Notes**:
```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = ctx =>
    {
        var token = ctx.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(token) &&
            ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/admin"))
            ctx.Token = token;
        return Task.CompletedTask;
    }
};
// Hub mapping:
app.MapHub<AdminNotificationHub>("/hubs/admin").RequireAuthorization("AdminPolicy");
```

## R7: Health Check Endpoints — Liveness vs. Readiness

**Decision**: Built-in `AddHealthChecks()` with three custom `IHealthCheck` implementations (MSSQL via `AddDbContextCheck<VendorDbContext>`, `RedisHealthCheck`, `PaymentGatewayHealthCheck`). Two endpoints filtered by predicate.

**Rationale**: ASP.NET Core's built-in health check infrastructure supports named checks, tag filtering, and `HealthCheckOptions` for Kubernetes liveness/readiness separation. No third-party packages needed beyond the built-in.

**Alternatives Considered**:
- Single `/health` endpoint: doesn't distinguish liveness from readiness; Kubernetes probes require both.
- `AspNetCore.Diagnostics.HealthChecks` library: convenient OOTB checks but adds external dependency.

**Implementation Notes**:
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VendorDbContext>("mssql")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<PaymentGatewayHealthCheck>("payments");

// Liveness — always 200, no dependency checks
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness — all registered checks
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => true });
```
