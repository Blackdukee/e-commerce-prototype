using Vendor.Domain.Aggregates.Payment;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IWebhookEventRepository
{
    Task<WebhookEventEntry?> GetByGatewayAndEventIdAsync(string gatewayName, string eventId, CancellationToken ct = default);
    Task AddAsync(WebhookEventEntry webhookEvent, CancellationToken ct = default);
}
