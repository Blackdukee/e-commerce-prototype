namespace Vendor.Application.Common.Interfaces;

public record WebhookParseResult(
    bool IsValid,
    string EventId,
    string EventType,
    bool IsPaymentSuccess,
    string? FailureReason = null,
    Guid? OrderId = null,
    decimal Amount = 0,
    string Currency = "USD"
);

public interface IWebhookParserFactory
{
    WebhookParseResult ParseAndVerify(string provider, string rawBody, string signatureHeader);
}
