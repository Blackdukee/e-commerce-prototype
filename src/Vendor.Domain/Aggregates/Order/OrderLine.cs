using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Order;

public class OrderLine : Entity<Guid>
{
    public OrderId OrderId { get; private init; }
    public ProductVariantId ProductVariantId { get; private init; }
    public string ProductName { get; private init; } = null!;
    public string Sku { get; private init; } = null!;
    public int Quantity { get; private init; }
    public Money UnitPrice { get; private init; }

    public Money LineTotal => UnitPrice * Quantity;

    private OrderLine() : base(default!)
    {
    }

    public OrderLine(
        OrderId orderId,
        ProductVariantId productVariantId,
        string productName,
        string sku,
        int quantity,
        Money unitPrice) : base(Guid.NewGuid())
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName, nameof(productName));
        ArgumentException.ThrowIfNullOrWhiteSpace(sku, nameof(sku));

        if (quantity <= 0)
        {
            throw new BusinessRuleViolationException("OrderLine quantity must be greater than zero.", nameof(OrderLine));
        }

        OrderId = orderId;
        ProductVariantId = productVariantId;
        ProductName = productName.Trim();
        Sku = sku.Trim().ToUpperInvariant();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
