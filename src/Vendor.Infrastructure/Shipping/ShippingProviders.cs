using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Shipping;

public class FlatRateShippingProvider : IShippingProvider
{
    public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin,
        Address destination,
        Weight weight,
        Dimensions dimensions,
        CancellationToken ct = default)
    {
        IReadOnlyList<ShippingRate> rates = [new ShippingRate("FLAT", "Flat Rate Ground", new Money(5.00m, "USD"), TimeSpan.FromDays(3))];
        return Task.FromResult(rates);
    }

    public Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate,
        Address origin,
        Address destination,
        CancellationToken ct = default)
    {
        var tracking = $"FLAT-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        return Task.FromResult(new ShippingLabel(tracking, "https://flatrate.com/label.pdf", "FLAT_RATE"));
    }

    public Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber,
        string carrierCode,
        CancellationToken ct = default)
    {
        return Task.FromResult(new ShipmentTrackingInfo(trackingNumber, "InTransit", "Sorting Facility", DateTime.UtcNow));
    }
}

public class ShippoShippingProvider(HttpClient httpClient) : IShippingProvider
{
    public Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin,
        Address destination,
        Weight weight,
        Dimensions dimensions,
        CancellationToken ct = default)
    {
        IReadOnlyList<ShippingRate> rates = [new ShippingRate("SHIPPO_PRIORITY", "Shippo Priority Express", new Money(12.50m, "USD"), TimeSpan.FromDays(1))];
        return Task.FromResult(rates);
    }

    public Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate,
        Address origin,
        Address destination,
        CancellationToken ct = default)
    {
        var tracking = $"SHIPPO-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        return Task.FromResult(new ShippingLabel(tracking, "https://shippo.com/label.pdf", "SHIPPO"));
    }

    public Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber,
        string carrierCode,
        CancellationToken ct = default)
    {
        return Task.FromResult(new ShipmentTrackingInfo(trackingNumber, "OutForDelivery", "Local Post Office", DateTime.UtcNow));
    }
}
