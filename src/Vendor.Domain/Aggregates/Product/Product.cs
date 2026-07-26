using Vendor.Domain.Abstractions;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Product;

public enum ProductStatus
{
    Draft,
    Active,
    Archived,
    Discontinued
}

public class Product : AggregateRoot<ProductId>
{
    private readonly List<ProductVariant> _variants = [];
    private readonly List<string> _images = [];

    public string Name { get; private set; }
    public Slug Slug { get; private set; }
    public string? Description { get; private set; }
    public Money BasePrice { get; private set; }
    public ProductStatus Status { get; private set; }
    public int LowStockThreshold { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyCollection<string> Images => _images.AsReadOnly();

    private Product() : base(default!)
    {
        Name = null!;
        Slug = default!;
    }

    public Product(
        ProductId id,
        string name,
        Slug slug,
        Money basePrice,
        string? description = null,
        int lowStockThreshold = 5) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (basePrice.Amount < 0m)
        {
            throw new BusinessRuleViolationException("Base price cannot be negative.", nameof(Product));
        }

        Name = name.Trim();
        Slug = slug;
        BasePrice = basePrice;
        Description = description?.Trim();
        Status = ProductStatus.Draft;
        LowStockThreshold = Math.Max(0, lowStockThreshold);
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void AddVariant(ProductVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant, nameof(variant));

        if (_variants.Any(v => string.Equals(v.Sku, variant.Sku, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleViolationException($"Variant with SKU '{variant.Sku}' already exists on product '{Name}'.", nameof(Product));
        }

        _variants.Add(variant);
    }

    public void AddImage(string imageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl, nameof(imageUrl));
        _images.Add(imageUrl.Trim());
    }

    public void Activate()
    {
        if (Status == ProductStatus.Active) return;

        if (BasePrice.Amount <= 0m)
        {
            throw new BusinessRuleViolationException("Cannot activate product with base price <= 0.", nameof(Product));
        }

        if (_images.Count == 0)
        {
            throw new BusinessRuleViolationException("Cannot activate product without at least one image.", nameof(Product));
        }

        Status = ProductStatus.Active;

        RaiseDomainEvent(new ProductActivatedEvent(Id, Name, BasePrice));
    }

    public void Discontinue()
    {
        Status = ProductStatus.Discontinued;
    }

    public void DeductVariantStock(ProductVariantId variantId, int quantity)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId);
        if (variant == null)
        {
            throw new BusinessRuleViolationException($"Variant '{variantId}' not found on product '{Name}'.", nameof(Product));
        }

        var lowStockEvent = variant.DeductStock(quantity, LowStockThreshold);
        if (lowStockEvent != null)
        {
            RaiseDomainEvent(lowStockEvent);
        }
    }

    public void DeductStock(ProductVariantId variantId, int quantity)
    {
        DeductVariantStock(variantId, quantity);
    }
}
