namespace Vendor.Domain.Aggregates.Payment;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public static PaymentId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
