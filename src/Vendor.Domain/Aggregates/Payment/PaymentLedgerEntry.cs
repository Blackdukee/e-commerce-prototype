using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Payment.Enums;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Payment;

public class PaymentLedgerEntry : Entity<Guid>
{
    public PaymentId PaymentId { get; private set; }
    public int SequenceNumber { get; private set; }
    public PaymentLedgerEventType EventType { get; private set; }
    public Money Amount { get; private set; }
    public string? GatewayReferenceId { get; private set; }
    public string? FailureReason { get; private set; }
    public string CorrelationId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PaymentLedgerEntry()
    {
        CorrelationId = null!;
        Amount = default;
    }

    public PaymentLedgerEntry(
        PaymentId paymentId,
        int sequenceNumber,
        PaymentLedgerEventType eventType,
        Money amount,
        string? gatewayReferenceId,
        string? failureReason,
        string correlationId)
        : base(Guid.NewGuid())
    {
        if (sequenceNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "Sequence number must be at least 1.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId, nameof(correlationId));

        PaymentId = paymentId;
        SequenceNumber = sequenceNumber;
        EventType = eventType;
        Amount = amount;
        GatewayReferenceId = gatewayReferenceId?.Trim();
        FailureReason = failureReason?.Trim();
        CorrelationId = correlationId.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
