using Vendor.Domain.Abstractions;

namespace Vendor.Application.Common.Interfaces;

public interface IOutboxService
{
    Task SaveAndPublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}
