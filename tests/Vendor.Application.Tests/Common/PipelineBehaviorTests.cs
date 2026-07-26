using FluentAssertions;
using Vendor.Application.Common.Results;

namespace Vendor.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Result_Success_PropertiesAreSetCorrectly()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Success_ReturnsValue()
    {
        var result = Result<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ResultT_Failure_ThrowsWhenAccessingValue()
    {
        var error = Error.NotFound("Product", "123");
        var result = Result<string>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
        result.Error.Type.Should().Be(ErrorType.NotFound);

        Action act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Result_ImplicitConversions_WorkAsExpected()
    {
        Result<int> successResult = 42;
        successResult.IsSuccess.Should().BeTrue();
        successResult.Value.Should().Be(42);

        Error error = Error.Failure("BAD_INPUT", "Invalid data");
        Result<int> failureResult = error;
        failureResult.IsFailure.Should().BeTrue();
        failureResult.Error.Code.Should().Be("BAD_INPUT");
    }

    [Fact]
    public void ResultFactory_CreateFailure_CreatesCorrectType()
    {
        var error = Error.NotFound("Cart", "ABC");
        var failure = ResultFactory.CreateFailure<Result<int>>(error);

        failure.IsFailure.Should().BeTrue();
        failure.Error.Should().Be(error);
    }
}
