using FluentAssertions;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Aggregates;

public class PaymentAndShipmentTests
{
    private static Address SampleAddress => new("123 Main St", "New York", "NY", "10001", "US");

    [Fact]
    public void Payment_PartialAndFullRefund_SucceedsUpToCapturedAmount()
    {
        var payment = new Payment(PaymentId.New(), OrderId.New(), new Money(100m, "USD"), "IDEMP-001");
        payment.Capture("TXN-123");

        payment.Refund(new Money(60m, "USD"));
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        payment.RefundedAmount.Amount.Should().Be(60m);

        payment.Refund(new Money(40m, "USD"));
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAmount.Amount.Should().Be(100m);
    }

    [Fact]
    public void Payment_OverRefund_ThrowsBusinessRuleViolationException()
    {
        var payment = new Payment(PaymentId.New(), OrderId.New(), new Money(100m, "USD"), "IDEMP-001");
        payment.Capture("TXN-123");

        payment.Refund(new Money(60m, "USD"));

        Action act = () => payment.Refund(new Money(50m, "USD")); // 60 + 50 = 110 > 100

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*cannot exceed captured amount*");
    }

    [Fact]
    public void Shipment_StatusProgression_AssignsTrackingOnLabelCreation()
    {
        var shipment = new Shipment(ShipmentId.New(), OrderId.New(), "SHIPPO", SampleAddress);
        shipment.Status.Should().Be(ShipmentStatus.Pending);
        shipment.TrackingNumber.Should().BeNull();

        shipment.CreateLabel("TRACK-999");
        shipment.Status.Should().Be(ShipmentStatus.LabelCreated);
        shipment.TrackingNumber.Should().Be("TRACK-999");

        shipment.MarkInTransit();
        shipment.Status.Should().Be(ShipmentStatus.InTransit);
        shipment.DomainEvents.Should().ContainSingle(e => e is ShipmentInTransitEvent);

        shipment.MarkOutForDelivery();
        shipment.MarkDelivered();
        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.DomainEvents.Should().Contain(e => e is ShipmentDeliveredEvent);
    }

    [Fact]
    public void Shipment_TransitionInTransitWithoutLabel_ThrowsException()
    {
        var shipment = new Shipment(ShipmentId.New(), OrderId.New(), "SHIPPO", SampleAddress);

        Action act = () => shipment.MarkInTransit();

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
