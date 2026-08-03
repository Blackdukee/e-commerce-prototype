using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Api.Endpoints;

public static class ShipmentRatesEndpoints
{
    public static RouteGroupBuilder MapShipmentRatesEndpoints(this RouteGroupBuilder group)
    {
        var shipments = group.MapGroup("/shipments").WithTags("Shipments");

        shipments.MapGet("/rates", async (
            string? originZip,
            string? destinationZip,
            int? weightGrams,
            IShippingProvider shippingProvider,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(originZip))
                return Results.BadRequest(new { Error = "originZip is required." });
            if (string.IsNullOrWhiteSpace(destinationZip))
                return Results.BadRequest(new { Error = "destinationZip is required." });
            if (!weightGrams.HasValue || weightGrams.Value <= 0)
                return Results.BadRequest(new { Error = "weightGrams must be a positive integer." });

            var origin = new Address("N/A", "N/A", "N/A", originZip, "US");
            var dest = new Address("N/A", "N/A", "N/A", destinationZip, "US");
            var weight = new Weight(weightGrams.Value / 1000m, WeightUnit.Kg);
            var dimensions = new Dimensions(10m, 10m, 10m, DimensionUnit.Cm);

            var rates = await shippingProvider.GetRatesAsync(origin, dest, weight, dimensions, ct);

            var dtos = rates.Select(r => new
            {
                serviceCode = r.ServiceCode,
                serviceName = r.ServiceName,
                amount = r.Cost.Amount,
                currency = r.Cost.Currency,
                estimatedDays = (int)r.EstimatedDeliveryTime.TotalDays
            });

            return Results.Ok(dtos);
        }).RequireAuthorization();

        return group;
    }
}
