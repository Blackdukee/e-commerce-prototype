using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Order;

public class Order : AggregateRoot<OrderId>
{
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled, OrderStatus.RefundRequested],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.RefundRequested],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.ReturnRequested, OrderStatus.ExchangeRequested],
        [OrderStatus.Delivered] = [OrderStatus.ReturnRequested, OrderStatus.ExchangeRequested],
        [OrderStatus.RefundRequested] = [OrderStatus.Refunded, OrderStatus.Confirmed, OrderStatus.Processing],
        [OrderStatus.ReturnRequested] = [OrderStatus.Returned, OrderStatus.Delivered],
        [OrderStatus.ExchangeRequested] = [OrderStatus.Exchanged, OrderStatus.Delivered],
        [OrderStatus.Refunded] = [],
        [OrderStatus.Returned] = [],
        [OrderStatus.Exchanged] = [],
        [OrderStatus.Cancelled] = []
    };

    private readonly List<OrderLine> _lines = [];

    public CustomerId CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string OrderNumber { get; private set; }
    public Address ShippingAddress { get; private set; }
    public Money Subtotal { get; private set; }
    public Money Tax { get; private set; }
    public Money ShippingCost { get; private set; }
    public Money Discount { get; private set; }
    public Money Total { get; private set; }
    public DateTime PlacedAtUtc { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    private Order() : base(default!)
    {
        OrderNumber = null!;
        ShippingAddress = null!;
    }

    public Order(
        OrderId id,
        CustomerId customerId,
        string orderNumber,
        Address shippingAddress,
        IEnumerable<OrderLine> lines,
        Money tax,
        Money shippingCost,
        Money discount) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber, nameof(orderNumber));
        ArgumentNullException.ThrowIfNull(shippingAddress, nameof(shippingAddress));
        ArgumentNullException.ThrowIfNull(lines, nameof(lines));

        var lineList = lines.ToList();
        if (lineList.Count == 0)
        {
            throw new BusinessRuleViolationException("An order must contain at least one order line.", nameof(Order));
        }

        _lines = lineList;
        CustomerId = customerId;
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        ShippingAddress = shippingAddress;
        Status = OrderStatus.Pending;
        PlacedAtUtc = DateTime.UtcNow;

        Tax = tax;
        ShippingCost = shippingCost;
        Discount = discount;

        // Calculate Subtotal
        var currency = lineList[0].UnitPrice.Currency;
        var subtotalAmount = lineList.Sum(l => l.LineTotal.Amount);
        Subtotal = new Money(subtotalAmount, currency);

        // Calculate Total = Subtotal + Tax + Shipping - Discount
        var totalAmount = Subtotal.Amount + Tax.Amount + ShippingCost.Amount - Discount.Amount;

        if (totalAmount < 0m)
        {
            throw new BusinessRuleViolationException(
                $"Order total cannot be negative. Calculated: {totalAmount}.",
                nameof(Order));
        }

        Total = new Money(totalAmount, currency);

        RaiseDomainEvent(new OrderPlacedEvent(Id, CustomerId, OrderNumber, Total, PlacedAtUtc));
    }

    public void ConfirmPayment()
    {
        EnsureCanTransitionTo(OrderStatus.Confirmed);
        Status = OrderStatus.Confirmed;
        RaiseDomainEvent(new OrderConfirmedEvent(Id, CustomerId, DateTime.UtcNow));
    }

    public void StartProcessing()
    {
        EnsureCanTransitionTo(OrderStatus.Processing);
        Status = OrderStatus.Processing;
    }

    public void Ship(ShipmentId shipmentId, string? trackingNumber = null)
    {
        EnsureCanTransitionTo(OrderStatus.Shipped);
        Status = OrderStatus.Shipped;
        RaiseDomainEvent(new OrderShippedEvent(Id, shipmentId, trackingNumber, DateTime.UtcNow));
    }

    public void Deliver()
    {
        EnsureCanTransitionTo(OrderStatus.Delivered);
        Status = OrderStatus.Delivered;
        RaiseDomainEvent(new OrderDeliveredEvent(Id, DateTime.UtcNow));
    }

    public void Cancel(string? reason = null)
    {
        EnsureCanTransitionTo(OrderStatus.Cancelled);
        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelledEvent(Id, reason, DateTime.UtcNow));
    }

    public void RequestRefund()
    {
        EnsureCanTransitionTo(OrderStatus.RefundRequested);
        Status = OrderStatus.RefundRequested;
        RaiseDomainEvent(new OrderRefundRequestedEvent(Id, DateTime.UtcNow));
    }

    public void Refund()
    {
        EnsureCanTransitionTo(OrderStatus.Refunded);
        Status = OrderStatus.Refunded;
    }

    public void RequestReturn()
    {
        EnsureCanTransitionTo(OrderStatus.ReturnRequested);
        Status = OrderStatus.ReturnRequested;
    }

    public void CompleteReturn()
    {
        EnsureCanTransitionTo(OrderStatus.Returned);
        Status = OrderStatus.Returned;
    }

    public void RequestExchange()
    {
        EnsureCanTransitionTo(OrderStatus.ExchangeRequested);
        Status = OrderStatus.ExchangeRequested;
    }

    public void CompleteExchange()
    {
        EnsureCanTransitionTo(OrderStatus.Exchanged);
        Status = OrderStatus.Exchanged;
    }

    private void EnsureCanTransitionTo(OrderStatus targetStatus)
    {
        if (!AllowedTransitions[Status].Contains(targetStatus))
        {
            throw new InvalidStateTransitionException(typeof(Order), Status, targetStatus);
        }
    }
}
