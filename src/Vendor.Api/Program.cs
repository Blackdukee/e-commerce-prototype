using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Vendor.Api.Extensions;
using Vendor.Api.Middleware;
using Vendor.Api.Security;
using Vendor.Infrastructure.Outbox;
using Vendor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Bootstrap Serilog logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add API, Application, and Infrastructure Services
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddApiLayerServices(builder.Configuration);
builder.Services.AddCustomRateLimiting();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Auto-apply EF Core database migrations on startup when using a relational database provider
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
    if (dbContext.Database.IsRelational() && !app.Environment.IsEnvironment("Testing"))
    {
        dbContext.Database.Migrate();
    }
}

// Ordered Middleware Pipeline (9 Stages)
// Stage 1: Exception Handler
app.UseExceptionHandler();
app.UseForwardedHeaders();

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

// Enable Static Files (Storefront & Mission Control UI)
app.UseDefaultFiles();
app.UseStaticFiles();

// Stage 7: Rate Limiting
app.UseRateLimiter();

// Stage 8: Maintenance Mode
app.UseMiddleware<MaintenanceModeMiddleware>();

// Stage 9: Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
    });

    RecurringJob.AddOrUpdate<OutboxProcessorJob>(
        "outbox-processor",
        job => job.ProcessOutboxMessagesAsync(CancellationToken.None),
        "*/5 * * * * *");

    RecurringJob.AddOrUpdate<OutboxCleanupJob>(
        "outbox-cleanup",
        job => job.PurgeOldProcessedMessagesAsync(CancellationToken.None),
        Cron.Daily(2));

    RecurringJob.AddOrUpdate<Vendor.Infrastructure.Search.ProductIndexSyncJob>(
        "product-index-sync",
        job => job.ExecuteAsync(CancellationToken.None),
        "*/5 * * * *");
}

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
