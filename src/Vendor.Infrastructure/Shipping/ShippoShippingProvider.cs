using System.Text;
using System.Text.Json;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Shipping;

public class ShippoShippingProvider(HttpClient httpClient, string apiKey) : IShippingProvider
{
    public async Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin, Address destination, Weight weight, Dimensions dimensions, CancellationToken ct = default)
    {
        var payload = new
        {
            address_from = new { street1 = origin.Street, city = origin.City, state = origin.State, zip = origin.ZipCode, country = origin.CountryCode },
            address_to = new { street1 = destination.Street, city = destination.City, state = destination.State, zip = destination.ZipCode, country = destination.CountryCode },
            parcels = new[]
            {
                new
                {
                    length = (double)dimensions.Length,
                    width = (double)dimensions.Width,
                    height = (double)dimensions.Height,
                    distance_unit = dimensions.Unit == DimensionUnit.Cm ? "cm" : "in",
                    weight = (double)(weight.Unit == WeightUnit.Kg ? weight.Value : weight.Value / 2.205m),
                    mass_unit = "kg"
                }
            },
            async_mode = false
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "shipments")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("ShippoToken", apiKey);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var rates = new List<ShippingRate>();

        foreach (var r in doc.RootElement.GetProperty("rates").EnumerateArray())
        {
            var code = r.GetProperty("servicelevel").GetProperty("token").GetString() ?? "";
            var name = r.GetProperty("servicelevel").GetProperty("name").GetString() ?? "";
            decimal.TryParse(r.GetProperty("amount").GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount);
            var currency = r.GetProperty("currency").GetString() ?? "USD";
            var days = r.TryGetProperty("estimated_days", out var d) && d.ValueKind == JsonValueKind.Number
                ? d.GetInt32() : 5;
            rates.Add(new ShippingRate(code, name, new Money(amount, currency), TimeSpan.FromDays(days)));
        }

        return rates;
    }

    public async Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate, Address origin, Address destination, CancellationToken ct = default)
    {
        var payload = new { rate = selectedRate.ServiceCode, async_mode = false };
        using var request = new HttpRequestMessage(HttpMethod.Post, "transactions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("ShippoToken", apiKey);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var tracking = doc.RootElement.GetProperty("tracking_number").GetString() ?? "";
        var labelUrl = doc.RootElement.GetProperty("label_url").GetString() ?? "";
        return new ShippingLabel(tracking, labelUrl, "SHIPPO");
    }

    public async Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber, string carrierCode, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"tracks/{carrierCode}/{trackingNumber}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("ShippoToken", apiKey);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.TryGetProperty("tracking_status", out var ts)
            ? ts.GetProperty("status").GetString() ?? "Unknown"
            : "Unknown";
        var location = doc.RootElement.TryGetProperty("tracking_status", out var ts2)
            && ts2.TryGetProperty("location", out var loc)
            ? loc.GetString() ?? ""
            : "";
        return new ShipmentTrackingInfo(trackingNumber, status, location, DateTime.UtcNow);
    }
}
