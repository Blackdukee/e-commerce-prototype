using Microsoft.Extensions.Logging;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Shipping;

public class HybridShippingProvider(
    FlatRateShippingProvider flatRateProvider,
    IShippingProvider? bostaProvider = null,
    ILogger<HybridShippingProvider>? logger = null) : IShippingProvider
{
    public async Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin, Address destination, Weight weight, Dimensions dimensions, CancellationToken ct = default)
    {
        if (bostaProvider is null)
            return await flatRateProvider.GetRatesAsync(origin, destination, weight, dimensions, ct);
        try
        {
            return await bostaProvider.GetRatesAsync(origin, destination, weight, dimensions, ct);
        }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "Bosta GetRatesAsync failed; falling back to flat rate.");
            return await flatRateProvider.GetRatesAsync(origin, destination, weight, dimensions, ct);
        }
    }

    public async Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate, Address origin, Address destination, CancellationToken ct = default)
    {
        if (bostaProvider is null)
            return await flatRateProvider.CreateLabelAsync(selectedRate, origin, destination, ct);
        try
        {
            return await bostaProvider.CreateLabelAsync(selectedRate, origin, destination, ct);
        }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "Bosta CreateLabelAsync failed; falling back to flat rate.");
            return await flatRateProvider.CreateLabelAsync(selectedRate, origin, destination, ct);
        }
    }

    public async Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber, string carrierCode, CancellationToken ct = default)
    {
        if (bostaProvider is null)
            return await flatRateProvider.TrackShipmentAsync(trackingNumber, carrierCode, ct);
        try
        {
            return await bostaProvider.TrackShipmentAsync(trackingNumber, carrierCode, ct);
        }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "Bosta TrackShipmentAsync failed; falling back to flat rate.");
            return await flatRateProvider.TrackShipmentAsync(trackingNumber, carrierCode, ct);
        }
    }
}
