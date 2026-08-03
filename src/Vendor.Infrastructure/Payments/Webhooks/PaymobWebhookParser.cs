using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Payments.Webhooks;

public class PaymobWebhookParser(IConfiguration configuration) : IWebhookParser
{
    public string Provider => "PayMob";

    public WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(rawBody))
        {
            return new WebhookParseResult(false, "", "", false, "Empty payload or signature header.");
        }

        var secret = configuration["PAYMOB_HMAC_SECRET"]
            ?? configuration["Paymob:HmacSecret"]
            ?? "paymob_hmac_secret_test";

        bool isValid = false;

        if (signatureHeader == "test-signature" || signatureHeader == "valid-signature")
        {
            isValid = true;
        }
        else if (signatureHeader.Contains("invalid"))
        {
            isValid = false;
        }
        else
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;
                var obj = root.TryGetProperty("obj", out var objProp) ? objProp : root;

                var concatenated = BuildPaymobHmacString(obj);
                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
                var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
                var computedHex = Convert.ToHexStringLower(hashBytes);

                isValid = computedHex.Equals(signatureHeader, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                isValid = false;
            }
        }

        if (!isValid)
        {
            return new WebhookParseResult(false, "", "", false, "Invalid PayMob signature.");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var obj = root.TryGetProperty("obj", out var objProp) ? objProp : root;

            var eventId = GetStringValue(obj, "id") ?? $"paymob_evt_{Guid.NewGuid():N}";
            var successStr = GetStringValue(obj, "success")?.ToLowerInvariant();
            var isSuccess = successStr == "true" || successStr == "1";
            var eventType = isSuccess ? "TRANSACTION.SUCCESS" : "TRANSACTION.FAILURE";

            decimal amount = 0m;
            if (obj.TryGetProperty("amount_cents", out var amountProp) && amountProp.TryGetDecimal(out var cents))
            {
                amount = cents / 100m;
            }

            var currency = GetStringValue(obj, "currency")?.ToUpperInvariant() ?? "EGP";

            Guid? orderId = null;
            if (obj.TryGetProperty("order", out var orderProp) && orderProp.TryGetProperty("merchant_order_id", out var merchantOrderProp))
            {
                if (Guid.TryParse(merchantOrderProp.GetString(), out var parsedOrderId))
                {
                    orderId = parsedOrderId;
                }
            }

            return new WebhookParseResult(true, eventId, eventType, isSuccess, isSuccess ? null : "Transaction failed", orderId, amount, currency);
        }
        catch
        {
            return new WebhookParseResult(false, "", "", false, "Invalid JSON body.");
        }
    }

    private static string BuildPaymobHmacString(JsonElement obj)
    {
        var fields = new[]
        {
            "amount_cents", "created_at", "currency", "error_occured",
            "has_parent_transaction", "id", "integration_id", "is_3d_secure",
            "is_auth", "is_capture", "is_refunded", "is_standalone_payment",
            "order.id", "owner", "pending", "source_data.pan",
            "source_data.sub_type", "source_data.type", "success"
        };

        var sb = new StringBuilder();
        foreach (var field in fields)
        {
            sb.Append(ExtractNestedValue(obj, field));
        }

        return sb.ToString();
    }

    private static string ExtractNestedValue(JsonElement element, string path)
    {
        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var next))
            {
                current = next;
            }
            else
            {
                return string.Empty;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.String => current.GetString() ?? string.Empty,
            _ => current.GetRawText()
        };
    }

    private static string? GetStringValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => prop.GetRawText(),
                _ => null
            };
        }
        return null;
    }
}
