using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Vendor.Api.Tests.Helpers;
using Vendor.Infrastructure.Payments.Webhooks;
using Xunit;

namespace Vendor.Api.Tests.Integration;

public class WebhookEndpointsTests : IClassFixture<VendorApiFactory>
{
    private readonly VendorApiFactory _factory;
    private const string TestStripeSecret = "whsec_test_secret_12345";
    private const string TestPaymobSecret = "paymob_hmac_secret_test";
    private const string TestPaypalWebhookId = "paypal_wh_id_test";

    public WebhookEndpointsTests(VendorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StripeWebhook_WithInvalidSignature_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", "t=123,v1=invalid_sig_hash");

        var response = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PaymobWebhook_WithInvalidSignature_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Paymob-HMAC", "invalid_hmac_hash");

        var response = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PaypalWebhook_WithInvalidSignature_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-ID", "trans_test_123");
        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-TIME", "2026-08-03T12:00:00Z");
        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-SIG", "invalid_paypal_sig");

        var response = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent("{}", Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StripeWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
    {
        var client = _factory.CreateClient();
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

        var signature = GenerateStripeSignature(payload, TestStripeSecret);
        client.DefaultRequestHeaders.Add("Stripe-Signature", signature);

        // First call
        var response1 = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(payload, Encoding.UTF8, "application/json"));
        var body1 = await response1.Content.ReadAsStringAsync();
        response1.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response body: {body1}");

        // Duplicate call (idempotency check)
        var response2 = await client.PostAsync("/api/v1/webhooks/stripe", new StringContent(payload, Encoding.UTF8, "application/json"));
        var body2 = await response2.Content.ReadAsStringAsync();
        response2.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Response body: {body2}");

    }

    [Fact]
    public async Task PaymobWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
    {
        var client = _factory.CreateClient();
        var payloadObj = new
        {
            type = "TRANSACTION",
            obj = new
            {
                id = 99887766,
                pending = false,
                amount_cents = 10000,
                success = true,
                is_auth = false,
                is_capture = true,
                is_standalone_payment = true,
                is_refunded = false,
                is_3d_secure = true,
                integration_id = 1234,
                profile_id = 5678,
                has_parent_transaction = false,
                order = new { id = 112233, merchant_order_id = Guid.NewGuid().ToString() },
                created_at = "2026-08-03T12:00:00.000000",
                currency = "EGP",
                error_occured = false,
                owner = 100,
                source_data = new { pan = "2345", sub_type = "MasterCard", type = "Card" }
            }
        };

        var payloadJson = JsonSerializer.Serialize(payloadObj);
        using var doc = JsonDocument.Parse(payloadJson);
        var concatenated = PaymobWebhookParser.BuildPaymobHmacString(doc.RootElement.GetProperty("obj"));

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(TestPaymobSecret));
        var validHmac = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated)));

        client.DefaultRequestHeaders.Add("Paymob-HMAC", validHmac);

        // First call
        var response1 = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent(payloadJson, Encoding.UTF8, "application/json"));
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Duplicate call (idempotency check)
        var response2 = await client.PostAsync("/api/v1/webhooks/paymob", new StringContent(payloadJson, Encoding.UTF8, "application/json"));
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PaypalWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry()
    {
        var client = _factory.CreateClient();
        var transmissionId = "trans_paypal_998877";
        var transmissionTime = "2026-08-03T12:00:00Z";

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

        var validSig = PaypalWebhookParser.GeneratePaypalSignature(transmissionId, transmissionTime, TestPaypalWebhookId, payload);

        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-ID", transmissionId);
        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-TIME", transmissionTime);
        client.DefaultRequestHeaders.Add("PAYPAL-TRANSMISSION-SIG", validSig);

        // First call
        var response1 = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent(payload, Encoding.UTF8, "application/json"));
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Duplicate call (idempotency check)
        var response2 = await client.PostAsync("/api/v1/webhooks/paypal", new StringContent(payload, Encoding.UTF8, "application/json"));
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string GenerateStripeSignature(string payload, string secret)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signature = Convert.ToHexStringLower(hash);
        return $"t={ts},v1={signature}";
    }
}
