using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Payments.Webhooks;

public interface IWebhookParser
{
    string Provider { get; }
    WebhookParseResult ParseAndVerify(string rawBody, string signatureHeader);
}
