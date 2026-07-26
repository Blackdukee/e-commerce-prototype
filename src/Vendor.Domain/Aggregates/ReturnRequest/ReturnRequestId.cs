namespace Vendor.Domain.Aggregates.ReturnRequest;

public readonly record struct ReturnRequestId(Guid Value)
{
    public static ReturnRequestId New() => new(Guid.NewGuid());
    public static ReturnRequestId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
