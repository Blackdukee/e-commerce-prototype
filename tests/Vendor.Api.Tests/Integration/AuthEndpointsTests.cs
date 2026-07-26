using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.DTOs;
using Vendor.Api.Tests.Helpers;

namespace Vendor.Api.Tests.Integration;

public class AuthEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(VendorApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidPayload_ReturnsCreated()
    {
        var email = $"john.{Guid.NewGuid():N}@example.com";
        var payload = new RegisterRequest(email, "John", "Doe", "Password123!");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOk()
    {
        var payload = new LoginRequest("admin@acme-store.com", "AdminPass123!");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GuestSession_CreatesSessionId()
    {
        var payload = new GuestSessionRequest(null);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/guest", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
