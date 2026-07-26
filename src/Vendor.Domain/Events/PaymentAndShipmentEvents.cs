using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Events;

public record PaymentCapturedEvent(PaymentId PaymentId, OrderId OrderId, Money Amount, string GatewayTransactionId, DateTime CapturedAtUtc) : DomainEvent;

public record PaymentFailedEvent(PaymentId PaymentId, OrderId OrderId, string FailureReason) : DomainEvent;

public record PaymentRefundedEvent(PaymentId PaymentId, OrderId OrderId, Money RefundAmount, Money TotalRefunded, DateTime RefundedAtUtc) : DomainEvent;

public record ShipmentInTransitEvent(ShipmentId ShipmentId, OrderId OrderId, string TrackingNumber, string CarrierCode, DateTime ShippedAtUtc) : DomainEvent;

public record ShipmentDeliveredEvent(ShipmentId ShipmentId, OrderId OrderId, DateTime DeliveredAtUtc) : DomainEvent;
