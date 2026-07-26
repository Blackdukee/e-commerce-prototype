using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.DTOs;
using Vendor.Api.Tests.Helpers;

namespace Vendor.Api.Tests.Integration;

public class AdminAndHealthEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public AdminAndHealthEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAdminConfig_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/config");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAdminConfig_WithAdminRole_ReturnsOk()
    {
        var client = _factory.CreateClient();
        client.WithAdminBearerToken();

        var response = await client.GetAsync("/api/v1/admin/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ValidatePromotion_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var payload = new ValidatePromotionRequest("SAVE10");
        var response = await client.PostAsJsonAsync("/api/v1/promotions/validate", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
