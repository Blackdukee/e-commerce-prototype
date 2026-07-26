using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Vendor.Api.Extensions;
using Vendor.Api.Middleware;
using Vendor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Bootstrap Serilog logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add API, Application, and Infrastructure Services
builder.Services.AddApiLayerServices(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Auto-apply EF Core database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
    dbContext.Database.Migrate();
}

// Ordered Middleware Pipeline (9 Stages)
// Stage 1: Exception Handler
app.UseExceptionHandler();

// Stage 2: Security Headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// Stage 3: Correlation ID Propagation
app.UseMiddleware<CorrelationIdMiddleware>();

// Stage 4: Structured Request Logging
app.UseSerilogRequestLogging();

// Stage 5: Response Compression
app.UseResponseCompression();

// Stage 6: CORS
app.UseCors("VendorCorsPolicy");

// Stage 7: Rate Limiting
app.UseRateLimiter();

// Stage 8: Maintenance Mode
app.UseMiddleware<MaintenanceModeMiddleware>();

// Stage 9: Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI in Development / Local
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure API Versioning & Endpoints
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapAllEndpoints(apiVersionSet);

app.Run();

public partial class Program { }
