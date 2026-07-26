using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.DTOs;
using Vendor.Api.Tests.Helpers;

namespace Vendor.Api.Tests.Integration;

public class CartEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public CartEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCart_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Checkout_ValidPayload_ReturnsCreated()
    {
        var client = _factory.CreateClient();
        client.WithCustomerBearerToken();

        var payload = new CheckoutRequest(
            new AddressDto("123 Main St", "NYC", "NY", "10001", "US"),
            "STANDARD",
            "stripe"
        );
        var response = await client.PostAsJsonAsync("/api/v1/orders/checkout", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
