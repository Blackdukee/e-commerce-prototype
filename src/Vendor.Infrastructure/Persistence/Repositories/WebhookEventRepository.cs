using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Entities;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Infrastructure.Persistence.Repositories;

public class WebhookEventRepository(VendorDbContext context) : IWebhookEventRepository
{
    public async Task<bool> ExistsAsync(string provider, string eventId, CancellationToken ct = default)
    {
        return await context.WebhookEvents
            .AnyAsync(w => w.Provider == provider && w.EventId == eventId, ct);
    }

    public async Task AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default)
    {
        await context.WebhookEvents.AddAsync(webhookEvent, ct);
        await context.SaveChangesAsync(ct);
    }

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
