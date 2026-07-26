using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Payment;

public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Failed,
    PartiallyRefunded,
    Refunded
}

public class Payment : AggregateRoot<PaymentId>
{
    public OrderId OrderId { get; private set; }
    public PaymentStatus Status { get; private set; }
    public Money Amount { get; private set; }
    public Money RefundedAmount { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? CapturedAtUtc { get; private set; }

    private Payment() : base(default!)
    {
        IdempotencyKey = null!;
    }

    public Payment(PaymentId id, OrderId orderId, Money amount, string idempotencyKey) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));

        if (amount.Amount <= 0m)
        {
            throw new BusinessRuleViolationException("Payment amount must be greater than zero.", nameof(Payment));
        }

        OrderId = orderId;
        Amount = amount;
        RefundedAmount = Money.Zero(amount.Currency);
        IdempotencyKey = idempotencyKey.Trim();
        Status = PaymentStatus.Pending;
    }

    public void Authorize()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new InvalidStateTransitionException(typeof(Payment), Status, PaymentStatus.Authorized);
        }

        Status = PaymentStatus.Authorized;
    }

    public void Capture(string gatewayTransactionId, DateTime? capturedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayTransactionId, nameof(gatewayTransactionId));

        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Authorized)
        {
            throw new InvalidStateTransitionException(typeof(Payment), Status, PaymentStatus.Captured);
        }

        GatewayTransactionId = gatewayTransactionId.Trim();
        Status = PaymentStatus.Captured;
        CapturedAtUtc = capturedAt ?? DateTime.UtcNow;

        RaiseDomainEvent(new PaymentCapturedEvent(Id, OrderId, Amount, GatewayTransactionId, CapturedAtUtc.Value));
    }

    public void Fail(string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason, nameof(failureReason));

        if (Status == PaymentStatus.Captured || Status == PaymentStatus.Refunded)
        {
            throw new BusinessRuleViolationException($"Cannot fail payment that is in '{Status}' status.", nameof(Payment));
        }

        Status = PaymentStatus.Failed;
        FailureReason = failureReason.Trim();

        RaiseDomainEvent(new PaymentFailedEvent(Id, OrderId, FailureReason));
    }

    public void Refund(Money refundAmount)
    {
        if (Status != PaymentStatus.Captured && Status != PaymentStatus.PartiallyRefunded)
        {
            throw new BusinessRuleViolationException($"Cannot refund payment in '{Status}' status.", nameof(Payment));
        }

        if (refundAmount.Amount <= 0m)
        {
            throw new BusinessRuleViolationException("Refund amount must be greater than zero.", nameof(Payment));
        }

        var newTotalRefunded = RefundedAmount + refundAmount;

        if (newTotalRefunded.Amount > Amount.Amount)
        {
            throw new BusinessRuleViolationException(
                $"Cumulative refunds ({newTotalRefunded.Amount} {Amount.Currency}) cannot exceed captured amount ({Amount.Amount} {Amount.Currency}).",
                nameof(Payment));
        }

        RefundedAmount = newTotalRefunded;
        Status = (RefundedAmount.Amount == Amount.Amount) ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        RaiseDomainEvent(new PaymentRefundedEvent(Id, OrderId, refundAmount, RefundedAmount, DateTime.UtcNow));
    }
}
