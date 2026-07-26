using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Interfaces.Adapters;

public record ShippingRate(string ServiceCode, string ServiceName, Money Cost, TimeSpan EstimatedDeliveryTime);
public record ShippingLabel(string TrackingNumber, string LabelUrl, string CarrierCode);
public record ShipmentTrackingInfo(string TrackingNumber, string Status, string CurrentLocation, DateTime LastUpdatedUtc);

public interface IShippingProvider
{
    Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin,
        Address destination,
        Weight weight,
        Dimensions dimensions,
        CancellationToken ct = default);

    Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate,
        Address origin,
        Address destination,
        CancellationToken ct = default);

    Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber,
        string carrierCode,
        CancellationToken ct = default);
}
