using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.DTOs;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class RateLimitingTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public RateLimitingTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthEndpoint_ExceedingLimit_Returns429TooManyRequests()
    {
        // Arrange - create client with isolated IP / context if needed
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest("test@example.com", "Password123!");

        HttpResponseMessage? lastResponse = null;

        // Act - Send 7 requests (auth-policy allows 5 per minute)
        for (int i = 0; i < 7; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        }

        // Assert
        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task CartCheckoutEndpoint_ExceedingLimit_Returns429TooManyRequests()
    {
        // Arrange - cart-checkout-policy allows 30 requests per minute
        var client = _factory.CreateClient();
        var cartId = Guid.NewGuid();

        HttpResponseMessage? lastResponse = null;

        // Act - Send 32 requests to cart endpoint
        for (int i = 0; i < 32; i++)
        {
            lastResponse = await client.GetAsync($"/api/v1/cart?cartId={cartId}");
        }

        // Assert
        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
