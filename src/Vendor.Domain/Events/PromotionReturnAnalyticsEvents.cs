using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Promotion;
using Vendor.Domain.Aggregates.ReturnRequest;

namespace Vendor.Domain.Events;

public record PromotionExhaustedEvent(PromotionId PromotionId, string Code, int FinalUsageCount, DateTime ExhaustedAtUtc) : DomainEvent;

public record ReturnRequestCreatedEvent(ReturnRequestId ReturnRequestId, OrderId OrderId, CustomerId CustomerId, int ItemCount) : DomainEvent;

public record ReturnRequestApprovedEvent(ReturnRequestId ReturnRequestId, ResolutionType ResolutionType, DateTime ApprovedAtUtc) : DomainEvent;

public record ReturnCompletedEvent(ReturnRequestId ReturnRequestId, OrderId OrderId, DateTime CompletedAtUtc) : DomainEvent;

public record ExchangeCompletedEvent(ReturnRequestId ReturnRequestId, OrderId OrderId, OrderId? ReplacementOrderId, DateTime CompletedAtUtc) : DomainEvent;
