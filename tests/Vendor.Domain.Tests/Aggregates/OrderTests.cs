using FluentAssertions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Aggregates;

public class OrderTests
{
    private static Address SampleAddress => new("123 Main St", "New York", "NY", "10001", "US");

    [Fact]
    public void Order_Creation_CalculatesTotalCorrectlyAndRaisesOrderPlacedEvent()
    {
        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var line1 = new OrderLine(orderId, ProductVariantId.New(), "Item 1", "SKU1", 2, new Money(50m, "USD")); // Subtotal = $100
        var tax = new Money(10m, "USD");
        var shipping = new Money(5m, "USD");
        var discount = new Money(15m, "USD");

        var order = new Order(orderId, customerId, "ACM-20260725-001", SampleAddress, [line1], tax, shipping, discount);

        order.Subtotal.Amount.Should().Be(100m);
        order.Total.Amount.Should().Be(100m); // 100 + 10 + 5 - 15 = 100
        order.Status.Should().Be(OrderStatus.Pending);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedEvent);
    }

    [Fact]
    public void Order_Creation_WithZeroLines_ThrowsBusinessRuleViolationException()
    {
        var orderId = OrderId.New();
        var customerId = CustomerId.New();

        Action act = () => _ = new Order(orderId, customerId, "ACM-001", SampleAddress, [], Money.Zero("USD"), Money.Zero("USD"), Money.Zero("USD"));

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*at least one order line*");
    }

    [Fact]
    public void Order_Creation_WithNullOrEmptyOrderNumber_ThrowsArgumentException()
    {
        var orderId = OrderId.New();
        var line = new OrderLine(orderId, ProductVariantId.New(), "Item 1", "SKU1", 1, new Money(10m, "USD"));

        Action act = () => _ = new Order(orderId, CustomerId.New(), "", SampleAddress, [line], Money.Zero("USD"), Money.Zero("USD"), Money.Zero("USD"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Order_ExcessiveDiscount_ThrowsBusinessRuleViolationException()
    {
        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var line1 = new OrderLine(orderId, ProductVariantId.New(), "Item 1", "SKU1", 1, new Money(10m, "USD"));
        var tax = Money.Zero("USD");
        var shipping = Money.Zero("USD");
        var discount = new Money(50m, "USD"); // Subtotal 10 - 50 = -40

        Action act = () => _ = new Order(orderId, customerId, "ACM-001", SampleAddress, [line1], tax, shipping, discount);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("*total cannot be negative*");
    }

    [Fact]
    public void Order_ValidStateTransitions_Succeed()
    {
        var order = CreateSampleOrder();

        order.ConfirmPayment();
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().Contain(e => e is OrderConfirmedEvent);

        order.StartProcessing();
        order.Status.Should().Be(OrderStatus.Processing);

        var shipmentId = ShipmentId.New();
        order.Ship(shipmentId, "TRACK123");
        order.Status.Should().Be(OrderStatus.Shipped);
        order.DomainEvents.Should().Contain(e => e is OrderShippedEvent);

        order.Deliver();
        order.Status.Should().Be(OrderStatus.Delivered);
        order.DomainEvents.Should().Contain(e => e is OrderDeliveredEvent);
    }

    [Fact]
    public void Order_CancelFlow_Succeeds()
    {
        var order = CreateSampleOrder();
        order.Cancel("Customer requested");
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().Contain(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Order_RefundFlow_Succeeds()
    {
        var order = CreateSampleOrder();
        order.ConfirmPayment();
        order.RequestRefund();
        order.Status.Should().Be(OrderStatus.RefundRequested);
        order.Refund();
        order.Status.Should().Be(OrderStatus.Refunded);
    }

    [Fact]
    public void Order_ReturnFlow_Succeeds()
    {
        var order = CreateSampleOrder();
        order.ConfirmPayment();
        order.StartProcessing();
        order.Ship(ShipmentId.New(), "TRK123");
        order.Deliver();

        order.RequestReturn();
        order.Status.Should().Be(OrderStatus.ReturnRequested);
        order.CompleteReturn();
        order.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public void Order_ExchangeFlow_Succeeds()
    {
        var order = CreateSampleOrder();
        order.ConfirmPayment();
        order.StartProcessing();
        order.Ship(ShipmentId.New(), "TRK123");
        order.Deliver();

        order.RequestExchange();
        order.Status.Should().Be(OrderStatus.ExchangeRequested);
        order.CompleteExchange();
        order.Status.Should().Be(OrderStatus.Exchanged);
    }

    [Fact]
    public void Order_InvalidStateTransition_ThrowsInvalidStateTransitionException()
    {
        var order = CreateSampleOrder();

        Action act = () => order.Deliver();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    private static Order CreateSampleOrder()
    {
        var orderId = OrderId.New();
        var line = new OrderLine(orderId, ProductVariantId.New(), "Item 1", "SKU1", 1, new Money(100m, "USD"));
        return new Order(orderId, CustomerId.New(), "ACM-001", SampleAddress, [line], Money.Zero("USD"), Money.Zero("USD"), Money.Zero("USD"));
    }
}
