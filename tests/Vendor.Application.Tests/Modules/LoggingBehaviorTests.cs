using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Vendor.Application.Common.Behaviors;
using Vendor.Application.Interfaces;

namespace Vendor.Application.Tests.Modules;

public class LoggingBehaviorTests
{
    public record TestRequest : IRequest<string>;

    [Fact]
    public async Task Handle_ExecutesNextAndLogsInformation()
    {
        var loggerMock = new Mock<ILogger<LoggingBehavior<TestRequest, string>>>();
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("user-123");

        var behavior = new LoggingBehavior<TestRequest, string>(loggerMock.Object, userMock.Object);

        RequestHandlerDelegate<string> next = () => Task.FromResult("Result");

        var response = await behavior.Handle(new TestRequest(), next, CancellationToken.None);

        response.Should().Be("Result");
    }
}
