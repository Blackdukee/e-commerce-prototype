using FluentAssertions;
using Moq;
using Vendor.Application.Commands.Payments.ProcessWebhook;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Tests.Payments;

public class ProcessWebhookCommandHandlerTests
{
    private readonly Mock<IPaymentGateway> _gatewayMock = new();
    private readonly Mock<IWebhookEventRepository> _webhookRepoMock = new();
    private readonly Mock<IPaymentLedgerRepository> _ledgerRepoMock = new();
    private readonly Mock<IPaymentRepository> _paymentRepoMock = new();

    public ProcessWebhookCommandHandlerTests()
    {
        _gatewayMock
            .Setup(g => g.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task Handle_InvalidSignature_ReturnsUnauthorizedError()
    {
        _gatewayMock
            .Setup(g => g.VerifyWebhookSignatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ProcessWebhookCommandHandler(_gatewayMock.Object, _webhookRepoMock.Object, _ledgerRepoMock.Object, _paymentRepoMock.Object);
        var command = new ProcessWebhookCommand("Stripe", "invalid-sig", "{}", "evt_100", "payment_intent.succeeded", Guid.NewGuid(), 100m, "USD", "pi_100");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Unauthorized");
    }

    [Fact]
    public async Task Handle_DuplicateEventId_ReturnsSkippedDuplicateStatus()
    {
        var existingEvent = new WebhookEventEntry("Stripe", "evt_100", "payment_intent.succeeded", "hash123");
        _webhookRepoMock
            .Setup(w => w.GetByGatewayAndEventIdAsync("Stripe", "evt_100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        var handler = new ProcessWebhookCommandHandler(_gatewayMock.Object, _webhookRepoMock.Object, _ledgerRepoMock.Object, _paymentRepoMock.Object);
        var command = new ProcessWebhookCommand("Stripe", "valid-sig", "{}", "evt_100", "payment_intent.succeeded", Guid.NewGuid(), 100m, "USD", "pi_100");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("SkippedDuplicate");
    }
}
