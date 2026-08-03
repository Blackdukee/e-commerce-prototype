using Vendor.Application.Common.Interfaces;

namespace Vendor.Infrastructure.Payments.Webhooks;

public class WebhookParserFactory(IEnumerable<IWebhookParser> parsers) : IWebhookParserFactory
{
    public WebhookParseResult ParseAndVerify(string provider, string rawBody, string signatureHeader)
    {
        var parser = parsers.FirstOrDefault(p => p.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (parser is null)
        {
            return new WebhookParseResult(false, "", "", false, $"Unsupported webhook provider: {provider}");
        }

        return parser.ParseAndVerify(rawBody, signatureHeader);
    }
}
