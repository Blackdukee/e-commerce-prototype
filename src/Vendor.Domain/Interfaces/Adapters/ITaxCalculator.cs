using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Interfaces.Adapters;

public interface ITaxCalculator
{
    Task<Money> CalculateTaxAsync(
        IReadOnlyList<OrderLine> lines,
        Address shippingAddress,
        string currencyCode,
        CancellationToken ct = default);
}
