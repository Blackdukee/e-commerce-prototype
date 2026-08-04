using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Tax;

public class FlatTaxCalculator(decimal vatRate = 0.14m) : ITaxCalculator
{
    public Task<Money> CalculateTaxAsync(
        IReadOnlyList<OrderLine> lines,
        Address shippingAddress,
        string currencyCode,
        CancellationToken ct = default)
    {
        var subtotal = lines.Sum(l => l.LineTotal.Amount);
        var taxAmount = Math.Round(subtotal * vatRate, 2);
        return Task.FromResult(new Money(taxAmount, currencyCode));
    }
}
