using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Events;

public record ProductActivatedEvent(ProductId ProductId, string Name, Money BasePrice) : DomainEvent;

public record ProductDeactivatedEvent(ProductId ProductId, string? Reason = null) : DomainEvent;

public record ProductLowStockEvent(
    ProductId ProductId,
    ProductVariantId ProductVariantId,
    string Sku,
    int CurrentStock,
    int Threshold) : DomainEvent;
