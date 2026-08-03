using Vendor.Domain.Abstractions;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Product;

public class ProductVariant : Entity<ProductVariantId>
{
    public ProductId ProductId { get; private set; }
    public string Sku { get; private set; }
    public Money PriceAdjustment { get; private set; }
    public int StockQuantity { get; private set; }
    public Weight Weight { get; private set; }
    public Dimensions Dimensions { get; private set; }

    private ProductVariant() : base(default!)
    {
        Sku = null!;
    }

    public ProductVariant(
        ProductVariantId id,
        ProductId productId,
        string sku,
        Money priceAdjustment,
        int stockQuantity,
        Weight weight,
        Dimensions dimensions) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku, nameof(sku));

        if (stockQuantity < 0)
        {
            throw new BusinessRuleViolationException("Stock quantity cannot be negative.", nameof(ProductVariant));
        }

        ProductId = productId;
        Sku = sku.Trim().ToUpperInvariant();
        PriceAdjustment = priceAdjustment;
        StockQuantity = stockQuantity;
        Weight = weight;
        Dimensions = dimensions;
    }

    public IDomainEvent? DeductStock(int quantity, int lowStockThreshold)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleViolationException("Deduction quantity must be positive.", nameof(ProductVariant));
        }

        if (StockQuantity - quantity < 0)
        {
            throw new BusinessRuleViolationException(
                $"Insufficient stock for SKU '{Sku}'. Available: {StockQuantity}, requested: {quantity}.",
                nameof(ProductVariant));
        }

        StockQuantity -= quantity;

        if (StockQuantity < lowStockThreshold)
        {
            return new ProductLowStockEvent(ProductId, Id, Sku, StockQuantity, lowStockThreshold);
        }

        return null;
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleViolationException("Quantity to add must be positive.", nameof(ProductVariant));
        }

        StockQuantity += quantity;
    }

    public void UpdateDetails(Money priceAdjustment, int stockQuantity, Weight weight)
    {
        if (stockQuantity < 0)
        {
            throw new BusinessRuleViolationException("Stock quantity cannot be negative.", nameof(ProductVariant));
        }

        PriceAdjustment = priceAdjustment;
        StockQuantity = stockQuantity;
        Weight = weight;
    }
}
