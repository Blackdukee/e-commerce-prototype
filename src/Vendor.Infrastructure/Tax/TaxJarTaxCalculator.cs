using System.Text;
using System.Text.Json;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Tax;

public class TaxJarTaxCalculator(HttpClient httpClient, string apiKey) : ITaxCalculator
{
    public async Task<Money> CalculateTaxAsync(
        IReadOnlyList<OrderLine> lines,
        Address shippingAddress,
        string currencyCode,
        CancellationToken ct = default)
    {
        var subtotal = lines.Sum(l => l.LineTotal.Amount);

        var payload = new
        {
            to_zip = shippingAddress.ZipCode,
            to_state = shippingAddress.State,
            to_country = shippingAddress.CountryCode,
            amount = (double)subtotal,
            shipping = 0,
            line_items = lines.Select(l => new
            {
                id = l.Sku,
                quantity = l.Quantity,
                unit_price = (double)l.UnitPrice.Amount
            }).ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "taxes")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var taxAmount = doc.RootElement
            .GetProperty("tax")
            .GetProperty("amount_to_collect")
            .GetDecimal();

        return new Money(taxAmount, currencyCode);
    }
}
