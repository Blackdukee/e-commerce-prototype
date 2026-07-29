using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;

namespace Vendor.Api.Tests.Integration;

public class OrderAndPaymentEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public OrderAndPaymentEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WebhookStripe_ReturnsOk()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", "test-signature");

        var response = await client.PostAsJsonAsync("/api/v1/webhooks/stripe", new { type = "payment_intent.succeeded" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShippingRates_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            origin = new { street = "100 Origin St", city = "NYC", state = "NY", zipCode = "10001", countryCode = "US" },
            destination = new { street = "200 Dest St", city = "LA", state = "CA", zipCode = "90001", countryCode = "US" },
            weightKg = 1.5,
            lengthCm = 20,
            widthCm = 15,
            heightCm = 10
        };

        var response = await client.PostAsJsonAsync("/api/v1/shipments/rates", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
