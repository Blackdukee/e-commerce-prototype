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
        // Arrange - isolate client partition using X-Forwarded-For header
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");

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
        // Arrange - isolate client partition using X-Forwarded-For header
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.2");

        var cartId = Guid.NewGuid();
        HttpResponseMessage? lastResponse = null;

        // Act - Send 32 requests to cart endpoint (cart-checkout-policy allows 30 per minute)
        for (int i = 0; i < 32; i++)
        {
            lastResponse = await client.GetAsync($"/api/v1/cart?cartId={cartId}");
        }

        // Assert
        lastResponse.Should().NotBeNull();
        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task DifferentClientIPs_HaveIndependentRateLimits()
    {
        // Arrange - client 1 exhausts auth limit for IP 10.0.0.10
        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.10");
        var loginRequest = new LoginRequest("test@example.com", "Password123!");

        for (int i = 0; i < 6; i++)
        {
            await client1.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        }

        // Act - client 2 sends request from a different IP 10.0.0.11
        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.11");
        var responseClient2 = await client2.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert - client 2 request should NOT be rate limited (not 429)
        responseClient2.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }
}
