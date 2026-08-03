using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Stripe;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Payments.Webhooks;

public class StripeWebhookParser(IConfiguration configuration) : IWebhookParser
{
    public string Provider => "Stripe";

    public WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(rawBody))
        {
            return new WebhookParseResult(false, "", "", false, "Empty payload or signature header.");
        }

        var secret = configuration["STRIPE_WEBHOOK_SECRET"]
            ?? configuration["Stripe:WebhookSecret"]
            ?? "whsec_test_secret_12345";

        bool isValid = VerifyStripeSignature(rawBody, signatureHeader, secret);

        if (!isValid)
        {
            return new WebhookParseResult(false, "", "", false, "Invalid Stripe webhook signature.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? $"evt_{Guid.NewGuid():N}" : $"evt_{Guid.NewGuid():N}";
            var eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "payment_intent.succeeded" : "payment_intent.succeeded";

            var isSuccess = eventType.Contains("succeeded", StringComparison.OrdinalIgnoreCase) ||
                            eventType.Contains("created", StringComparison.OrdinalIgnoreCase);

            Guid? orderId = null;
            decimal amount = 0m;
            string currency = "USD";

            if (root.TryGetProperty("data", out var dataProp) && dataProp.TryGetProperty("object", out var objProp))
            {
                if (objProp.TryGetProperty("amount", out var amountProp))
                {
                    var amtText = amountProp.ValueKind == JsonValueKind.String ? amountProp.GetString() : amountProp.GetRawText();
                    if (!string.IsNullOrEmpty(amtText) && decimal.TryParse(amtText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rawAmount))
                    {
                        amount = rawAmount > 100 ? rawAmount / 100m : rawAmount;
                    }
                }

                if (objProp.TryGetProperty("currency", out var currProp))
                {
                    currency = currProp.GetString()?.ToUpperInvariant() ?? "USD";
                }

                if (objProp.TryGetProperty("metadata", out var metaProp) && metaProp.TryGetProperty("order_id", out var orderIdProp))
                {
                    if (Guid.TryParse(orderIdProp.GetString(), out var parsedOrderId))
                    {
                        orderId = parsedOrderId;
                    }
                }
            }

            return new WebhookParseResult(true, eventId, eventType, isSuccess, isSuccess ? null : "Payment failed", orderId, amount, currency);
        }
        catch
        {
            return new WebhookParseResult(false, "", "", false, "Invalid JSON body.");
        }
    }

    public static bool VerifyStripeSignature(string rawBody, string signatureHeader, string secret, long toleranceSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(rawBody)) return false;

        var parts = signatureHeader.Split(',');
        string? timestampStr = null;
        string? expectedSig = null;

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
            {
                var key = kv[0].Trim();
                var val = kv[1].Trim();
                if (key.Equals("t", StringComparison.OrdinalIgnoreCase))
                {
                    timestampStr = val;
                }
                else if (key.Equals("v1", StringComparison.OrdinalIgnoreCase))
                {
                    expectedSig = val;
                }
            }
        }

        if (string.IsNullOrEmpty(timestampStr) || string.IsNullOrEmpty(expectedSig))
        {
            return false;
        }

        if (!long.TryParse(timestampStr, out var timestamp))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > toleranceSeconds)
        {
            return false;
        }

        var signedPayload = $"{timestampStr}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var computedSig = Convert.ToHexStringLower(hashBytes);

        return computedSig.Equals(expectedSig, StringComparison.OrdinalIgnoreCase);
    }
}
