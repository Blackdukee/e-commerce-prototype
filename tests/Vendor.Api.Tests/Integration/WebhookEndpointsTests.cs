using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class WebhookEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;

    public WebhookEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StripeWebhook_WithInvalidSignature_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", "t=123,v1=invalid_sig");

        var response = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PaymobWebhook_WithInvalidSignature_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Paymob-HMAC", "invalid_hmac");

        var response = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PaypalWebhook_WithInvalidSignature_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-ID", "invalid_trans_id");

        var response = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StripeWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", "test-signature");

        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_stripe_test_100",
            type = "payment_intent.succeeded",
            data = new
            {
                @object = new
                {
                    id = "pi_123456",
                    amount = 5000,
                    currency = "usd"
                }
            }
        });

        // First call
        var response1 = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(payload, Encoding.UTF8, "application/json"));
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Duplicate call (idempotency check)
        var response2 = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(payload, Encoding.UTF8, "application/json"));
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PaymobWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Paymob-HMAC", "test-signature");

        var payload = JsonSerializer.Serialize(new
        {
            type = "TRANSACTION",
            obj = new
            {
                id = 99887766,
                success = true,
                amount_cents = 10000,
                currency = "EGP",
                error_occured = false
            }
        });

        // First call
        var response1 = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent(payload, Encoding.UTF8, "application/json"));
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Duplicate call (idempotency check)
        var response2 = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent(payload, Encoding.UTF8, "application/json"));
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PaypalWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-ID", "test-signature");

        var payload = JsonSerializer.Serialize(new
        {
            id = "WH-PAYPAL-12345",
            event_type = "PAYMENT.CAPTURE.COMPLETED",
            resource = new
            {
                amount = new
                {
                    value = "150.00",
                    currency_code = "USD"
                }
            }
        });

        // First call
        var response1 = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent(payload, Encoding.UTF8, "application/json"));
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Duplicate call (idempotency check)
        var response2 = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent(payload, Encoding.UTF8, "application/json"));
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
