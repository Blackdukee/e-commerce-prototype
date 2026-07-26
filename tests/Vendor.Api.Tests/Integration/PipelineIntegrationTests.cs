using System.Net;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;

namespace Vendor.Api.Tests.Integration;

public class PipelineIntegrationTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public PipelineIntegrationTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessHealthCheck_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_GeneratesAndPropagatesCorrelationIdHeader()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/products");

        response.Headers.Should().ContainKey("X-Correlation-ID");
        response.Headers.GetValues("X-Correlation-ID").First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Request_SetsSecurityHeaders()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/products");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");
    }

    [Fact]
    public async Task ApiVersioning_ReturnsSupportedVersionsHeader()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/products");

        response.Headers.Should().ContainKey("api-supported-versions");
        response.Headers.GetValues("api-supported-versions").First().Should().Contain("1.0");
    }
}
