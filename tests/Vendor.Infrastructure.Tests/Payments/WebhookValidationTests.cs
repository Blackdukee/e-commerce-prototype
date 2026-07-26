using FluentAssertions;
using Vendor.Infrastructure.Payments;

namespace Vendor.Infrastructure.Tests.Payments;

public class WebhookValidationTests
{
    [Fact]
    public void Paymob_VerifyHmac_ValidSignature_ReturnsTrue()
    {
        var secret = "secret_key_123";
        var payload = new Dictionary<string, string>
        {
            ["amount_cents"] = "1000",
            ["created_at"] = "2026-07-25T12:00:00Z",
            ["currency"] = "USD",
            ["error_occured"] = "false",
            ["has_parent_transaction"] = "false",
            ["id"] = "999",
            ["integration_id"] = "1",
            ["is_3d_secure"] = "true",
            ["is_auth"] = "false",
            ["is_capture"] = "true",
            ["is_refunded"] = "false",
            ["is_standalone_payment"] = "true",
            ["order.id"] = "555",
            ["owner"] = "acme",
            ["pending"] = "false",
            ["source_data.pan"] = "4111",
            ["source_data.sub_type"] = "card",
            ["source_data.type"] = "card",
            ["success"] = "true"
        };

        // Compute expected HMAC for test payload
        var keysToConcatenate = new[]
        {
            "amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction",
            "id", "integration_id", "is_3d_secure", "is_auth", "is_capture", "is_refunded",
            "is_standalone_payment", "order.id", "owner", "pending", "source_data.pan",
            "source_data.sub_type", "source_data.type", "success"
        };

        var sb = new System.Text.StringBuilder();
        foreach (var k in keysToConcatenate) sb.Append(payload[k]);

        using var hmac = new System.Security.Cryptography.HMACSHA512(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();

        var isValid = PaymobPaymentGateway.VerifyPaymobHmac(payload, secret, hash);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Paymob_VerifyHmac_InvalidSignature_ReturnsFalse()
    {
        var payload = new Dictionary<string, string> { ["amount_cents"] = "1000" };
        var isValid = PaymobPaymentGateway.VerifyPaymobHmac(payload, "secret", "invalid_hash");
        isValid.Should().BeFalse();
    }
}
