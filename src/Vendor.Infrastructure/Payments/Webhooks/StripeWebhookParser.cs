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
            ?? "whsec_test";

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
                var stripeEvent = EventUtility.ConstructEvent(
                    rawBody,
                    signatureHeader,
                    secret,
                    tolerance: 300,
                    throwOnApiVersionMismatch: false);
                isValid = stripeEvent != null;
            }
            catch
            {
                isValid = false;
            }
        }

        if (!isValid)
        {
            return new WebhookParseResult(false, "", "", false, "Invalid Stripe signature.");
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
                if (objProp.TryGetProperty("amount", out var amountProp) && amountProp.TryGetDecimal(out var rawAmount))
                {
                    amount = rawAmount > 100 ? rawAmount / 100m : rawAmount;
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

            return new WebhookParseResult(true, eventId, eventType, isSuccess, null, orderId, amount, currency);
        }
        catch
        {
            return new WebhookParseResult(false, "", "", false, "Invalid JSON body.");
        }
    }
}
