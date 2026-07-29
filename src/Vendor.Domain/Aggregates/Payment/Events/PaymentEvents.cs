using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Payment.Events;

public record PaymentIntentCreatedEvent(PaymentId PaymentId, OrderId OrderId, Money Amount, DateTime OccurredAtUtc) : DomainEvent;
