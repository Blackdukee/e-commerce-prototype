namespace Vendor.Domain.Aggregates.AnalyticsEvent;

public readonly record struct AnalyticsEventId(Guid Value)
{
    public static AnalyticsEventId New() => new(Guid.NewGuid());
    public static AnalyticsEventId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
