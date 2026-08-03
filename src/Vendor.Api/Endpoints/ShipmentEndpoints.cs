using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Api.Extensions;
using Vendor.Application.Modules.Shipments;

namespace Vendor.Api.Endpoints;

public static class ShipmentEndpoints
{
    public static RouteGroupBuilder MapShipmentEndpoints(this RouteGroupBuilder group)
    {
        var shipments = group.MapGroup("/shipments")
            .WithTags("Shipments");

        shipments.MapPost("/rates", async (ShippingRatesRequest req, ISender mediator, CancellationToken ct) =>
        {
            return Results.Ok(new ShippingRatesResponseDto(new[]
            {
                new ShippingRateDto("STANDARD", "Standard Shipping", new MoneyDto(5.99m, "USD"), 3, 5),
                new ShippingRateDto("EXPRESS", "Express Shipping", new MoneyDto(15.99m, "USD"), 1, 2)
            }));
        });

        shipments.MapGet("/track/{trackingNumber}", async (string trackingNumber, string? carrierCode, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new TrackShipmentQuery(trackingNumber, carrierCode ?? "STANDARD"), ct);
            return result.ToHttpResult();
        });

        shipments.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetShipmentByIdQuery(id), ct);
            return result.ToHttpResult();
        });

        var adminShipments = group.MapGroup("/admin/shipments")
            .WithTags("Admin Shipments")
            .RequireAuthorization();

        adminShipments.MapPost("/", async (CreateShipmentRequest req, ISender mediator, CancellationToken ct) =>
        {
            var command = new CreateShipmentLabelCommand(req.OrderId, req.CarrierCode, $"TRK-{Guid.NewGuid():N}".Substring(0, 12).ToUpperInvariant());
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        adminShipments.MapPost("/{id:guid}/ship", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new MarkShipmentInTransitCommand(id), ct);
            return result.ToHttpResult();
        });

        adminShipments.MapPost("/{id:guid}/deliver", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new MarkShipmentDeliveredCommand(id), ct);
            return result.ToHttpResult();
        });

        return group;
    }

    private record ShippingRatesResponseDto(ShippingRateDto[] Rates);
}
