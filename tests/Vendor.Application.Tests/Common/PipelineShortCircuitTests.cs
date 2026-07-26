using FluentAssertions;
using FluentValidation;
using MediatR;
using NSubstitute;
using Vendor.Application.Common.Behaviors;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;

namespace Vendor.Application.Tests.Common;

public class PipelineShortCircuitTests
{
    public record TestCommand(string Name, int Quantity) : ICommand<Result<string>>;

    public class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be > 0.");
        }
    }

    public record TestIdempotentCommand(string IdempotencyKey) : IIdempotentRequest<Result<string>>;

    [Fact]
    public async Task ValidationBehavior_InvalidPayload_ShortCircuitsWithValidationError422()
    {
        var validator = new TestCommandValidator();
        var behavior = new ValidationBehavior<TestCommand, Result<string>>([validator]);

        var command = new TestCommand("", -1);
        RequestHandlerDelegate<Result<string>> next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        var validationError = result.Error.Should().BeOfType<ValidationError>().Subject;
        validationError.Errors.Should().ContainKey("Name");
        validationError.Errors.Should().ContainKey("Quantity");

        await next.DidNotReceive().Invoke();
    }

    [Fact]
    public async Task IdempotencyBehavior_DuplicateKey_ReturnsCachedResultWithoutCallingNext()
    {
        var store = Substitute.For<IIdempotencyStore>();
        var cached = Result<string>.Success("cached-value");
        store.GetResultAsync<Result<string>>("KEY-123", Arg.Any<CancellationToken>())
            .Returns(cached);

        var behavior = new IdempotencyBehavior<TestIdempotentCommand, Result<string>>(store);
        var command = new TestIdempotentCommand("KEY-123");
        RequestHandlerDelegate<Result<string>> next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("cached-value");
        await next.DidNotReceive().Invoke();
    }

    [Fact]
    public async Task TransactionBehavior_HandlerFails_RollsBackTransaction()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionBehavior<TestCommand, Result<string>>>.Instance;
        var behavior = new TransactionBehavior<TestCommand, Result<string>>(unitOfWork, logger);

        var command = new TestCommand("Item", 5);
        RequestHandlerDelegate<Result<string>> next = () => Task.FromResult(Result<string>.Failure(Error.Failure("ERR", "Failed")));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
