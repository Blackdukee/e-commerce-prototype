namespace Vendor.Domain.Aggregates.Shipment;

public readonly record struct ShipmentId(Guid Value)
{
    public static ShipmentId New() => new(Guid.NewGuid());
    public static ShipmentId Empty => new(Guid.Empty);
    public override string ToString() => Value.ToString();
}
