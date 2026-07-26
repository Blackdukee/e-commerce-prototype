using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vendor.Domain.Abstractions;

namespace Vendor.Infrastructure.Outbox;

public sealed class OutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var dbContext = eventData.Context;

        var outboxMessages = new List<OutboxMessage>();

        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            var entityType = entry.Entity.GetType();
            var domainEventsProp = entityType.GetProperty("DomainEvents", BindingFlags.Public | BindingFlags.Instance);
            var clearEventsMethod = entityType.GetMethod("ClearDomainEvents", BindingFlags.Public | BindingFlags.Instance);

            if (domainEventsProp != null && clearEventsMethod != null)
            {
                if (domainEventsProp.GetValue(entry.Entity) is IEnumerable<IDomainEvent> events)
                {
                    var eventList = events.ToList();
                    if (eventList.Count > 0)
                    {
                        clearEventsMethod.Invoke(entry.Entity, null);

                        foreach (var domainEvent in eventList)
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
        }

        if (outboxMessages.Count > 0)
        {
            dbContext.Set<OutboxMessage>().AddRange(outboxMessages);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
