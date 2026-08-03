using Microsoft.EntityFrameworkCore;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Outbox;

public class OutboxCleanupJob(VendorDbContext dbContext)
{
    public async Task PurgeOldProcessedMessagesAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var oldMessages = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processed && m.ProcessedAtUtc < cutoff)
            .ToListAsync(ct);

        if (oldMessages.Count > 0)
        {
            dbContext.OutboxMessages.RemoveRange(oldMessages);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
