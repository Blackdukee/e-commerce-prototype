namespace Vendor.Domain.Aggregates.Promotion;

public readonly record struct PromotionId(Guid Value)
{
    public static PromotionId New() => new(Guid.NewGuid());
    public static PromotionId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
