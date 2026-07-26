using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin")
            .WithTags("Admin & Settings")
            .RequireAuthorization();

        admin.MapGet("/analytics/summary", async (DateTime? from, DateTime? to, ISender mediator) =>
        {
            return Results.Ok(new AnalyticsSummaryDto(150, 14999.99m, 45, 12));
        });

        admin.MapGet("/settings", async (ISender mediator) =>
        {
            return Results.Ok(new { vendorId = "acme-store", displayName = "ACME Store" });
        });

        admin.MapPatch("/settings/branding", async (UpdateBrandingRequest req, ISender mediator) =>
        {
            return Results.Ok(req);
        });

        admin.MapPatch("/settings/checkout", async (UpdateCheckoutRequest req, ISender mediator) =>
        {
            return Results.Ok(req);
        });

        admin.MapPatch("/settings/shipping", async (UpdateShippingRequest req, ISender mediator) =>
        {
            return Results.Ok(req);
        });

        admin.MapPatch("/settings/feature-flags", async (UpdateFeatureFlagsRequest req, ISender mediator) =>
        {
            return Results.Ok(req);
        });

        admin.MapPost("/settings/maintenance", async (ToggleMaintenanceRequest req, ISender mediator) =>
        {
            return Results.NoContent();
        });

        return group;
    }
}
