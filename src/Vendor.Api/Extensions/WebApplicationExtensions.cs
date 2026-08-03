using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.Endpoints;
using Vendor.Infrastructure.Realtime;

namespace Vendor.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication MapAllEndpoints(this WebApplication app, ApiVersionSet versionSet)
    {
        // Versioned API v1.0 route group
        var v1 = app.MapGroup("/api/v{version:apiVersion}")
            .WithApiVersionSet(versionSet);

        // Chain module endpoint groups
        v1.MapAuthEndpoints();
        v1.MapCustomerEndpoints();
        v1.MapAdminCustomerEndpoints();
        v1.MapProductEndpoints();
        v1.MapCartEndpoints();
        v1.MapOrderEndpoints();
        v1.MapPaymentEndpoints();
        v1.MapShipmentEndpoints();
        v1.MapReturnEndpoints();
        v1.MapPromotionEndpoints();
        v1.MapAdminEndpoints();
        v1.MapVendorSettingsEndpoints();
        v1.MapWebhookEndpoints();

        // SignalR WebSockets Hub endpoint
        app.MapHub<AdminNotificationHub>("/hubs/admin");

        // Health Check Probes
        // Liveness probe (no-op, always 200 OK)
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        // Readiness probe (checks MSSQL DB connectivity, Redis, Payment gateway)
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true
        });

        return app;
    }
}
