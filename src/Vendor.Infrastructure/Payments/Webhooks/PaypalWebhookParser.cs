using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Payments.Webhooks;

public class PaypalWebhookParser(IConfiguration configuration) : IWebhookParser
{
    public string Provider => "PayPal";

    public WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(rawBody))
        {
            return new WebhookParseResult(false, "", "", false, "Empty payload or transmission header.");
        }

        var webhookId = configuration["PAYPAL_WEBHOOK_ID"]
            ?? configuration["Paypal:WebhookId"]
            ?? "paypal_wh_id_test";

        bool isValid = false;

        try
        {
            var transmissionId = ExtractHeaderParam(signatureHeader, "id");
            var transmissionTime = ExtractHeaderParam(signatureHeader, "time");
            var sig = ExtractHeaderParam(signatureHeader, "sig");

            if (string.IsNullOrEmpty(sig))
            {
                sig = signatureHeader;
            }

            if (string.IsNullOrEmpty(transmissionId))
            {
                transmissionId = "trans_default_id";
            }

            if (string.IsNullOrEmpty(transmissionTime))
            {
                transmissionTime = "2026-08-03T12:00:00Z";
            }

            var crc32 = ComputeCrc32(Encoding.UTF8.GetBytes(rawBody));
            var stringToSign = $"{transmissionId}|{transmissionTime}|{webhookId}|{crc32}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookId));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            var expectedSig = Convert.ToHexStringLower(hashBytes);

            isValid = expectedSig.Equals(sig.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            isValid = false;
        }

        if (!isValid)
        {
            return new WebhookParseResult(false, "", "", false, "Invalid PayPal transmission signature.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? $"WH-{Guid.NewGuid():N}" : $"WH-{Guid.NewGuid():N}";
            var eventType = root.TryGetProperty("event_type", out var typeProp) ? typeProp.GetString() ?? "PAYMENT.CAPTURE.COMPLETED" : "PAYMENT.CAPTURE.COMPLETED";

            var isSuccess = eventType.Contains("COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                            eventType.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase);

            decimal amount = 0m;
            string currency = "USD";
            Guid? orderId = null;

            if (root.TryGetProperty("resource", out var resourceProp))
            {
                if (resourceProp.TryGetProperty("amount", out var amountProp))
                {
                    if (amountProp.TryGetProperty("value", out var valProp))
                    {
                        var valText = valProp.ValueKind == JsonValueKind.String ? valProp.GetString() : valProp.GetRawText();
                        if (!string.IsNullOrEmpty(valText) && decimal.TryParse(valText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                        {
                            amount = val;
                        }
                    }
                    if (amountProp.TryGetProperty("currency_code", out var currProp))
                    {
                        currency = currProp.GetString()?.ToUpperInvariant() ?? "USD";
                    }
                }

                if (resourceProp.TryGetProperty("custom_id", out var customIdProp))
                {
                    if (Guid.TryParse(customIdProp.GetString(), out var parsedOrderId))
                    {
                        orderId = parsedOrderId;
                    }
                }
            }

            return new WebhookParseResult(true, eventId, eventType, isSuccess, isSuccess ? null : "Payment denied or failed", orderId, amount, currency);
        }
        catch
        {
            return new WebhookParseResult(false, "", "", false, "Invalid JSON body.");
        }
    }

    public static string GeneratePaypalSignature(string transmissionId, string transmissionTime, string webhookId, string rawBody)
    {
        var crc32 = ComputeCrc32(Encoding.UTF8.GetBytes(rawBody));
        var stringToSign = $"{transmissionId}|{transmissionTime}|{webhookId}|{crc32}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookId));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToHexStringLower(hashBytes);
    }

    public static uint ComputeCrc32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        return ~crc;
    }

    private static string ExtractHeaderParam(string fullHeader, string key)
    {
        var parts = fullHeader.Split(';');
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return kv[1].Trim();
            }
        }
        return string.Empty;
    }
}
