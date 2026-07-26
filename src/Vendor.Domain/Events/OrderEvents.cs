using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Events;

public record OrderPlacedEvent(OrderId OrderId, CustomerId CustomerId, string OrderNumber, Money Total, DateTime PlacedAtUtc) : DomainEvent;

public record OrderConfirmedEvent(OrderId OrderId, CustomerId CustomerId, DateTime ConfirmedAtUtc) : DomainEvent;

public record OrderShippedEvent(OrderId OrderId, ShipmentId ShipmentId, string? TrackingNumber, DateTime ShippedAtUtc) : DomainEvent;

public record OrderDeliveredEvent(OrderId OrderId, DateTime DeliveredAtUtc) : DomainEvent;

public record OrderCancelledEvent(OrderId OrderId, string? Reason, DateTime CancelledAtUtc) : DomainEvent;

public record OrderRefundRequestedEvent(OrderId OrderId, DateTime RequestedAtUtc) : DomainEvent;
