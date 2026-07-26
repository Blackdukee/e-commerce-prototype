using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Vendor.Api.Middleware;

namespace Vendor.Api.Tests.Unit;

public class MiddlewareTests
{
    [Fact]
    public async Task SecurityHeadersMiddleware_InjectsSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new SecurityHeadersMiddleware(next);

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'self'");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task CorrelationIdMiddleware_GeneratesCorrelationIdHeader_WhenMissing()
    {
        var context = new DefaultHttpContext();
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next);

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-ID"].ToString().Should().NotBeNullOrWhiteSpace();
        context.Items["CorrelationId"].Should().NotBeNull();
    }

    [Fact]
    public async Task CorrelationIdMiddleware_PropagatesExistingCorrelationIdHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "existing-cid-12345";
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next);

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-ID"].ToString().Should().Be("existing-cid-12345");
        context.Items["CorrelationId"].Should().Be("existing-cid-12345");
    }
}
