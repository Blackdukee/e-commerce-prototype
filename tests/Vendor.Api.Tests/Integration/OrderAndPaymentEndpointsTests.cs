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
        var rawPayload = "{\"type\":\"payment_intent.succeeded\",\"id\":\"evt_integration_100\"}";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{rawPayload}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes("whsec_test_secret_12345"));
        var sigHex = Convert.ToHexStringLower(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(signedPayload)));

        client.DefaultRequestHeaders.Add("Stripe-Signature", $"t={ts},v1={sigHex}");

        var response = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(rawPayload, System.Text.Encoding.UTF8, "application/json"));

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
