using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Outbox;

public class OutboxProcessorJob(VendorDbContext dbContext, IPublisher publisher)
{
    public async Task ProcessOutboxMessagesAsync(CancellationToken ct = default)
    {
        var messages = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type);
                if (type == null)
                {
                    message.MarkAsFailed($"Type '{message.Type}' could not be loaded.");
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(message.Content, type);
                if (domainEvent == null)
                {
                    message.MarkAsFailed($"Failed to deserialize outbox message payload.");
                    continue;
                }

                await publisher.Publish(domainEvent, ct);
                message.MarkAsProcessed();
            }
            catch (Exception ex)
            {
                message.MarkAsFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
