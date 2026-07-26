using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vendor.Domain.Interfaces;

namespace Vendor.Api.Middleware;

public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IVendorSettingsRepository settingsRepo)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Exempt routes: health checks and admin settings endpoints
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hubs/admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        try
        {
            var runtimeConfig = await settingsRepo.GetRuntimeConfigAsync("acme-store", context.RequestAborted);
            if (runtimeConfig?.FeatureFlags != null &&
                runtimeConfig.FeatureFlags.Flags.TryGetValue("maintenanceMode", out var isMaintenance) &&
                isMaintenance)
            {
                var correlationId = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier;
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Service Unavailable",
                    Detail = "The platform is currently undergoing scheduled maintenance. Please try again later.",
                    Instance = path,
                    Type = "https://httpstatuses.com/503"
                };
                problem.Extensions["correlationId"] = correlationId;
                await context.Response.WriteAsJsonAsync(problem);
                return;
            }
        }
        catch
        {
            // If DB/settings fail, pass through to let global exception handler capture
        }

        await next(context);
    }
}
