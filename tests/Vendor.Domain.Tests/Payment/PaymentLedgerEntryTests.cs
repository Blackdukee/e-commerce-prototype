using FluentAssertions;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Payment.Enums;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.Payments;

public class PaymentLedgerEntryTests
{
    [Fact]
    public void PaymentLedgerEntry_ValidParameters_CreatesEntrySuccessfully()
    {
        var paymentId = PaymentId.New();
        var money = new Money(150m, "USD");
        var correlationId = Guid.NewGuid().ToString("N");

        var entry = new PaymentLedgerEntry(
            paymentId,
            sequenceNumber: 1,
            eventType: PaymentLedgerEventType.Intent,
            amount: money,
            gatewayReferenceId: "ref-123",
            failureReason: null,
            correlationId: correlationId
        );

        entry.PaymentId.Should().Be(paymentId);
        entry.SequenceNumber.Should().Be(1);
        entry.EventType.Should().Be(PaymentLedgerEventType.Intent);
        entry.Amount.Amount.Should().Be(150m);
        entry.GatewayReferenceId.Should().Be("ref-123");
        entry.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void PaymentLedgerEntry_InvalidSequenceNumber_ThrowsException()
    {
        var paymentId = PaymentId.New();
        var money = new Money(150m, "USD");

        Action act = () => new PaymentLedgerEntry(
            paymentId,
            sequenceNumber: 0,
            eventType: PaymentLedgerEventType.Intent,
            amount: money,
            gatewayReferenceId: null,
            failureReason: null,
            correlationId: "corr-123"
        );

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
