using System.Text.Json;
using MediatR;
using Vendor.Application.Common.Interfaces;
using Vendor.Domain.Abstractions;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Outbox;

public class OutboxService(VendorDbContext dbContext, IPublisher publisher) : IOutboxService
{
    public async Task SaveAndPublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        var outboxMessage = new OutboxMessage
        {
            Id = domainEvent.EventId,
            Type = domainEvent.GetType().AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredOnUtc = domainEvent.OccurredOnUtc,
            RetryCount = 0
        };

        await dbContext.OutboxMessages.AddAsync(outboxMessage, ct);
        await dbContext.SaveChangesAsync(ct);
        await publisher.Publish(domainEvent, ct);
    }
}
