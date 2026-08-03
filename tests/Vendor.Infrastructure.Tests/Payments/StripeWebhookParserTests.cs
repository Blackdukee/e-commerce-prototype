using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Vendor.Infrastructure.Payments.Webhooks;
using Xunit;

namespace Vendor.Infrastructure.Tests.Payments;

public class StripeWebhookParserTests
{
    [Fact]
    public void StripeWebhookParser_ValidSignature_ParsesSuccessfully()
    {
        var secret = "whsec_test_secret_12345";
        var inMemorySettings = new Dictionary<string, string?> {
            {"STRIPE_WEBHOOK_SECRET", secret}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var parser = new StripeWebhookParser(configuration);

        var payload = "{\"id\":\"evt_123\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"amount\":5000,\"currency\":\"usd\"}}}";
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var hex = Convert.ToHexStringLower(hash);
        var sigHeader = $"t={ts},v1={hex}";

        var result = parser.ParseAndVerify(payload, sigHeader);

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal("evt_123", result.EventId);
        Assert.Equal("payment_intent.succeeded", result.EventType);
        Assert.True(result.IsPaymentSuccess);
    }
}
