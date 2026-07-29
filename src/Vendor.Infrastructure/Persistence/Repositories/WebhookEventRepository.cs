using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Infrastructure.Persistence.Repositories;

public class WebhookEventRepository(VendorDbContext context) : IWebhookEventRepository
{
    public async Task<WebhookEventEntry?> GetByGatewayAndEventIdAsync(string gatewayName, string eventId, CancellationToken ct = default)
    {
        return await context.WebhookEventEntries
            .FirstOrDefaultAsync(w => w.GatewayName == gatewayName && w.EventId == eventId, ct);
    }

    public async Task AddAsync(WebhookEventEntry webhookEvent, CancellationToken ct = default)
    {
        await context.WebhookEventEntries.AddAsync(webhookEvent, ct);
    }
}
