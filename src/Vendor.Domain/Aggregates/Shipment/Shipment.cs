using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Shipment;

public enum ShipmentStatus
{
    Pending,
    LabelCreated,
    InTransit,
    OutForDelivery,
    Delivered,
    Failed
}

public class Shipment : AggregateRoot<ShipmentId>
{
    public OrderId OrderId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string? TrackingNumber { get; private set; }
    public string CarrierCode { get; private set; }
    public Address ShippingAddress { get; private set; }
    public DateTime? EstimatedDeliveryUtc { get; private set; }
    public DateTime? ShippedAtUtc { get; private set; }

    private Shipment() : base(default!)
    {
        CarrierCode = null!;
        ShippingAddress = null!;
    }

    public Shipment(ShipmentId id, OrderId orderId, string carrierCode, Address shippingAddress) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrierCode, nameof(carrierCode));
        ArgumentNullException.ThrowIfNull(shippingAddress, nameof(shippingAddress));

        OrderId = orderId;
        CarrierCode = carrierCode.Trim().ToUpperInvariant();
        ShippingAddress = shippingAddress;
        Status = ShipmentStatus.Pending;
    }

    public void CreateLabel(string trackingNumber, DateTime? estimatedDelivery = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingNumber, nameof(trackingNumber));

        if (Status != ShipmentStatus.Pending)
        {
            throw new InvalidStateTransitionException(typeof(Shipment), Status, ShipmentStatus.LabelCreated);
        }

        TrackingNumber = trackingNumber.Trim();
        EstimatedDeliveryUtc = estimatedDelivery;
        Status = ShipmentStatus.LabelCreated;
    }

    public void MarkInTransit()
    {
        if (Status != ShipmentStatus.LabelCreated)
        {
            throw new InvalidStateTransitionException(typeof(Shipment), Status, ShipmentStatus.InTransit);
        }

        Status = ShipmentStatus.InTransit;
        ShippedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ShipmentInTransitEvent(Id, OrderId, TrackingNumber ?? "", CarrierCode, ShippedAtUtc.Value));
    }

    public void MarkOutForDelivery()
    {
        if (Status != ShipmentStatus.InTransit)
        {
            throw new InvalidStateTransitionException(typeof(Shipment), Status, ShipmentStatus.OutForDelivery);
        }

        Status = ShipmentStatus.OutForDelivery;
    }

    public void MarkDelivered()
    {
        if (Status != ShipmentStatus.OutForDelivery && Status != ShipmentStatus.InTransit)
        {
            throw new InvalidStateTransitionException(typeof(Shipment), Status, ShipmentStatus.Delivered);
        }

        Status = ShipmentStatus.Delivered;
        RaiseDomainEvent(new ShipmentDeliveredEvent(Id, OrderId, DateTime.UtcNow));
    }

    public void MarkFailed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (Status == ShipmentStatus.Delivered)
        {
            throw new BusinessRuleViolationException("Cannot fail a shipment that has already been delivered.", nameof(Shipment));
        }

        Status = ShipmentStatus.Failed;
    }
}
