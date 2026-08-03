using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vendor.Domain.Abstractions;

namespace Vendor.Infrastructure.Outbox;

public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ProcessOutboxEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ProcessOutboxEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ProcessOutboxEvents(DbContext? dbContext)
    {
        if (dbContext is null) return;

        var outboxMessages = new List<OutboxMessage>();

        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            if (entry.Entity is IHasDomainEvents entityWithEvents)
            {
                var events = entityWithEvents.DomainEvents.ToList();
                if (events.Count > 0)
                {
                    entityWithEvents.ClearDomainEvents();

                    foreach (var domainEvent in events)
                    {
                        outboxMessages.Add(new OutboxMessage
                        {
                            Id = domainEvent.EventId,
                            Type = domainEvent.GetType().AssemblyQualifiedName!,
                            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                            OccurredOnUtc = domainEvent.OccurredOnUtc,
                            RetryCount = 0
                        });
                    }
                }
            }
        }

        if (outboxMessages.Count > 0)
        {
            dbContext.Set<OutboxMessage>().AddRange(outboxMessages);
        }
    }
}
