using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class ShipmentEndpoints
{
    public static RouteGroupBuilder MapShipmentEndpoints(this RouteGroupBuilder group)
    {
        var shipments = group.MapGroup("/shipments")
            .WithTags("Shipments");

        shipments.MapPost("/rates", async (ShippingRatesRequest req, ISender mediator) =>
        {
            return Results.Ok(new ShippingRatesResponseDto(new[]
            {
                new ShippingRateDto("STANDARD", "Standard Shipping", new MoneyDto(5.99m, "USD"), 3, 5),
                new ShippingRateDto("EXPRESS", "Express Shipping", new MoneyDto(15.99m, "USD"), 1, 2)
            }));
        });

        shipments.MapGet("/track/{trackingNumber}", async (string trackingNumber, ISender mediator) =>
        {
            return Results.Ok(new TrackingResponseDto(trackingNumber, "InTransit", "Distribution Center", DateTime.UtcNow));
        });

        var adminShipments = group.MapGroup("/admin/shipments")
            .WithTags("Admin Shipments")
            .RequireAuthorization();

        adminShipments.MapPost("/", async (CreateShipmentRequest req, ISender mediator) =>
        {
            return Results.Created($"/api/v1/shipments/{Guid.NewGuid()}", new ShipmentDto(Guid.NewGuid(), req.OrderId, null, null, req.CarrierCode, "Created"));
        });

        adminShipments.MapPost("/{id:guid}/label", async (Guid id, ISender mediator) =>
        {
            return Results.Ok(new ShipmentDto(id, Guid.NewGuid(), "1Z9999999999", "https://labels.shippo.com/123.pdf", "UPS", "LabelCreated"));
        });

        adminShipments.MapPost("/{id:guid}/ship", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminShipments.MapPost("/{id:guid}/deliver", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        return group;
    }

    private record ShippingRatesResponseDto(ShippingRateDto[] Rates);
}
