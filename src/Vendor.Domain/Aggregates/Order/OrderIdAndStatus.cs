namespace Vendor.Domain.Aggregates.Order;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    RefundRequested,
    Refunded,
    ReturnRequested,
    ExchangeRequested,
    Returned,
    Exchanged
}
