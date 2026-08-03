using Microsoft.Extensions.Logging;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Tax;

public class HybridTaxCalculator(
    FlatTaxCalculator flatTaxCalculator,
    ITaxCalculator? taxJarCalculator = null,
    ILogger<HybridTaxCalculator>? logger = null) : ITaxCalculator
{
    public async Task<Money> CalculateTaxAsync(
        IReadOnlyList<OrderLine> lines,
        Address shippingAddress,
        string currencyCode,
        CancellationToken ct = default)
    {
        if (taxJarCalculator is null)
            return await flatTaxCalculator.CalculateTaxAsync(lines, shippingAddress, currencyCode, ct);

        try
        {
            return await taxJarCalculator.CalculateTaxAsync(lines, shippingAddress, currencyCode, ct);
        }
        catch (HttpRequestException ex)
        {
            logger?.LogWarning(ex, "TaxJar CalculateTaxAsync failed; falling back to flat rate.");
            return await flatTaxCalculator.CalculateTaxAsync(lines, shippingAddress, currencyCode, ct);
        }
    }
}
