namespace Vendor.Domain.Aggregates.Product;

public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
    public static ProductId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}

public readonly record struct ProductVariantId(Guid Value)
{
    public static ProductVariantId New() => new(Guid.NewGuid());
    public static ProductVariantId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
