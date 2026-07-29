using FluentAssertions;
using Moq;
using Vendor.Application.Queries.Payments.GetPaymentLedger;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Payment.Enums;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Tests.Payments;

public class GetPaymentLedgerQueryHandlerTests
{
    private readonly Mock<IPaymentLedgerRepository> _ledgerRepoMock = new();
    private readonly Mock<IPaymentRepository> _paymentRepoMock = new();

    [Fact]
    public async Task Handle_PaymentNotFound_ReturnsNotFoundError()
    {
        var paymentId = Guid.NewGuid();
        _ledgerRepoMock
            .Setup(r => r.GetByPaymentIdAsync(It.IsAny<PaymentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentLedgerEntry>());

        var handler = new GetPaymentLedgerQueryHandler(_ledgerRepoMock.Object, _paymentRepoMock.Object);
        var result = await handler.Handle(new GetPaymentLedgerQuery(paymentId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.NotFound");
    }

    [Fact]
    public async Task Handle_ValidPayment_ReturnsTimelineInSequenceOrder()
    {
        var paymentId = PaymentId.New();
        var money = new Money(100m, "USD");
        var correlationId = Guid.NewGuid().ToString("N");

        var entries = new List<PaymentLedgerEntry>
        {
            new(paymentId, 1, PaymentLedgerEventType.Intent, money, null, null, correlationId),
            new(paymentId, 2, PaymentLedgerEventType.Authorized, money, "gtw_123", null, correlationId)
        };

        _ledgerRepoMock
            .Setup(r => r.GetByPaymentIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var handler = new GetPaymentLedgerQueryHandler(_ledgerRepoMock.Object, _paymentRepoMock.Object);
        var result = await handler.Handle(new GetPaymentLedgerQuery(paymentId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PaymentId.Should().Be(paymentId.Value);
        result.Value.Timeline.Should().HaveCount(2);
        result.Value.Timeline[0].SequenceNumber.Should().Be(1);
        result.Value.Timeline[0].EventType.Should().Be("Intent");
        result.Value.Timeline[1].SequenceNumber.Should().Be(2);
        result.Value.Timeline[1].EventType.Should().Be("Authorized");
    }
}
