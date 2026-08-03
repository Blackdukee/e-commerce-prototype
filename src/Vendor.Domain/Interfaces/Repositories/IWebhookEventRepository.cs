using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Entities;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IWebhookEventRepository
{
    Task<bool> ExistsAsync(string provider, string eventId, CancellationToken ct = default);
    Task AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default);

    Task<WebhookEventEntry?> GetByGatewayAndEventIdAsync(string gatewayName, string eventId, CancellationToken ct = default);
    Task AddAsync(WebhookEventEntry webhookEvent, CancellationToken ct = default);
}

