using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using Vendor.Application.Common.Behaviors;
using Vendor.Application.Common.Results;

namespace Vendor.Application.Tests.Modules;

public class ValidationBehaviorTests
{
    public record DummyRequest : IRequest<Result>;

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<DummyRequest, Result>(Enumerable.Empty<IValidator<DummyRequest>>());
        var request = new DummyRequest();
        var nextCalled = false;

        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        var result = await behavior.Handle(request, next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidationFails_ReturnsFailureResultWithoutCallingNext()
    {
        var validatorMock = new Mock<IValidator<DummyRequest>>();
        var validationFailure = new ValidationFailure("TestProp", "Test error");

        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<DummyRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { validationFailure }));

        var behavior = new ValidationBehavior<DummyRequest, Result>([validatorMock.Object]);
        var request = new DummyRequest();
        var nextCalled = false;

        RequestHandlerDelegate<Result> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        var result = await behavior.Handle(request, next, CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Failure");
    }
}
