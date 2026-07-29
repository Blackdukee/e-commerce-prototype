using FluentAssertions;
using MediatR;
using Moq;
using Vendor.Application.Common.Behaviors;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Tests.Payments;

public record TestIdempotentCommand(string IdempotencyKey, decimal Amount) : IIdempotentRequest<Result<string>>;

public class IdempotencyBehaviorTests
{
    private readonly Mock<IIdempotencyStore> _storeMock = new();
    private readonly Mock<IPaymentIdempotencyRepository> _repoMock = new();
    private readonly Mock<IIdempotencyLockManager> _lockManagerMock = new();

    public IdempotencyBehaviorTests()
    {
        _lockManagerMock
            .Setup(l => l.AcquireLockAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IDisposable>().Object);
    }

    [Fact]
    public async Task Handle_InvalidUuidKey_ReturnsFailureResult()
    {
        var behavior = new IdempotencyBehavior<TestIdempotentCommand, Result<string>>(_storeMock.Object, _repoMock.Object, _lockManagerMock.Object);
        var command = new TestIdempotentCommand("invalid-uuid-format", 100m);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_IDEMPOTENCY_KEY");
    }

    [Fact]
    public async Task Handle_PayloadMismatch_ReturnsPayloadMismatchError()
    {
        var keyUuid = Guid.NewGuid();
        var originalKey = new PaymentIdempotencyKey(keyUuid, "different_hash_string");

        _repoMock.Setup(r => r.GetByKeyUuidAsync(keyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalKey);

        var behavior = new IdempotencyBehavior<TestIdempotentCommand, Result<string>>(_storeMock.Object, _repoMock.Object, _lockManagerMock.Object);
        var command = new TestIdempotentCommand(keyUuid.ToString(), 100m);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("ok"));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("IDEMPOTENCY_PAYLOAD_MISMATCH");
    }

    [Fact]
    public async Task Handle_NewKey_ExecutesNextAndSavesKey()
    {
        var keyUuid = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByKeyUuidAsync(keyUuid, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentIdempotencyKey?)null);

        var behavior = new IdempotencyBehavior<TestIdempotentCommand, Result<string>>(_storeMock.Object, _repoMock.Object, _lockManagerMock.Object);
        var command = new TestIdempotentCommand(keyUuid.ToString(), 100m);

        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Success("Executed Successfully"));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Executed Successfully");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<PaymentIdempotencyKey>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<PaymentIdempotencyKey>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
