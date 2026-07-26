using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Domain.Events;

public record CustomerCreatedEvent(CustomerId CustomerId, string Email, DateTime RegisteredAtUtc) : DomainEvent;

public record CustomerConsentUpdatedEvent(CustomerId CustomerId, bool AnalyticsConsent, DateTime UpdatedAtUtc) : DomainEvent;

public record CartAbandonedEvent(CartId CartId, CustomerId? CustomerId, DateTime LastModifiedUtc) : DomainEvent;

public record CustomerSuspendedEvent(CustomerId CustomerId, string Reason, DateTime SuspendedAtUtc, CustomerId SuspendedBy) : DomainEvent;

public record CustomerReactivatedEvent(CustomerId CustomerId, DateTime ReactivatedAtUtc, CustomerId ReactivatedBy) : DomainEvent;

public record CustomerRoleChangedEvent(CustomerId CustomerId, CustomerRole PreviousRole, CustomerRole NewRole, CustomerId ChangedBy, DateTime ChangedAtUtc) : DomainEvent;
