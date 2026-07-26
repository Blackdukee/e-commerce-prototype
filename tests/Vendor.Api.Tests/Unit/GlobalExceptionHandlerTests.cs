using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendor.Api.Middleware;
using Vendor.Domain.Exceptions;

namespace Vendor.Api.Tests.Unit;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_BusinessRuleViolationException_MapsTo409Conflict()
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Development");

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, envMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new BusinessRuleViolationException("Rule violated", "Order");

        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(409);
        problemDetails.Title.Should().Be("Business Rule Violation");
    }

    [Fact]
    public async Task TryHandleAsync_UnhandledException_MapsTo500InternalServerError()
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Production");

        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, envMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var exception = new InvalidOperationException("Unexpected failure");

        var result = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        result.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
