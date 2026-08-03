using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Vendor.Api.Security;
using Xunit;

namespace Vendor.Api.Tests.Unit;

public class HangfireDashboardAuthorizationFilterTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    public void Authorize_LoopbackIp_ReturnsTrue(string ipString, bool expectedResult)
    {
        // Arrange
        var filter = new HangfireDashboardAuthorizationFilter();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ipString);

        var storageMock = new Mock<JobStorage>();
        var dashboardContext = new AspNetCoreDashboardContext(storageMock.Object, new DashboardOptions(), httpContext);

        // Act
        var result = filter.Authorize(dashboardContext);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void Authorize_NonLoopbackIp_HostHeaderLocalhost_ReturnsFalse_WhenUnauthenticated()
    {
        // Arrange - Host header injection attempt with Host = "localhost", but RemoteIp = 192.168.1.50
        var filter = new HangfireDashboardAuthorizationFilter();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.50");
        httpContext.Request.Host = new HostString("localhost");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated

        var storageMock = new Mock<JobStorage>();
        var dashboardContext = new AspNetCoreDashboardContext(storageMock.Object, new DashboardOptions(), httpContext);

        // Act
        var result = filter.Authorize(dashboardContext);

        // Assert
        result.Should().BeFalse();
    }
}
