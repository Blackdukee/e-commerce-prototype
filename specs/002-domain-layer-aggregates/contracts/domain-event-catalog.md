# Contract: Domain Event Catalogue

**Feature**: 002-domain-layer-aggregates
**Scope**: All domain events raised by the 11 aggregate roots in `Vendor.Domain`.
**Transport**: Events are NOT dispatched directly. They are enqueued into the `OutboxMessages` table inside `SaveChangesAsync` by `Vendor.Infrastructure` and published asynchronously by `OutboxProcessor`.

---

## Event Envelope (all events implement `IDomainEvent`)

```csharp
public interface IDomainEvent
{
    Guid     EventId        { get; }   // Unique per instance — used for outbox idempotency
    DateTime OccurredOnUtc { get; }   // UTC timestamp at aggregate method call
}
```

All concrete events inherit from `DomainEvent` (abstract record):
```csharp
public abstract record DomainEvent : IDomainEvent
{
    public Guid     EventId        { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
```

---

## Event Registry

### Product Aggregate

#### `ProductActivatedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ProductId` | `ProductId` | The product that was activated |
| `Name` | `string` | Product name at activation |
| `BasePrice` | `Money` | Price snapshot |

**Triggered by**: `Product.Activate()`  
**Consumers** (Infrastructure/Application): catalog search index update, notification to merchandising team.

---

#### `ProductDeactivatedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ProductId` | `ProductId` | |
| `Reason` | `string?` | Optional deactivation reason |

**Triggered by**: `Product.Deactivate()`

---

#### `ProductLowStockEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ProductId` | `ProductId` | |
| `ProductVariantId` | `ProductVariantId` | Specific variant |
| `Sku` | `string` | Variant SKU |
| `CurrentStock` | `int` | Stock level after deduction |
| `Threshold` | `int` | Configured low-stock threshold |

**Triggered by**: `ProductVariant.DeductStock()` when result < threshold.

---

### Customer Aggregate

#### `CustomerCreatedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `CustomerId` | `CustomerId` | |
| `Email` | `string` | Registered email |
| `RegisteredAtUtc` | `DateTime` | |

**Triggered by**: `Customer.ConvertToRegistered()`

---

#### `CustomerConsentUpdatedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `CustomerId` | `CustomerId` | |
| `AnalyticsConsent` | `bool` | New consent value |
| `UpdatedAtUtc` | `DateTime` | |

**Triggered by**: `Customer.UpdateConsent()`

---

### Cart Aggregate

#### `CartAbandonedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `CartId` | `CartId` | |
| `CustomerId` | `CustomerId?` | Null for guest carts |
| `LastModifiedUtc` | `DateTime` | When cart was last touched |

**Triggered by**: `Cart.MarkAbandoned()`

---

### Order Aggregate

#### `OrderPlacedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `OrderId` | `OrderId` | |
| `CustomerId` | `CustomerId` | |
| `OrderNumber` | `string` | Human-readable order reference |
| `Total` | `Money` | |
| `PlacedAtUtc` | `DateTime` | |

---

#### `OrderConfirmedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `OrderId` | `OrderId` | |
| `CustomerId` | `CustomerId` | |
| `ConfirmedAtUtc` | `DateTime` | |

---

#### `OrderShippedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `OrderId` | `OrderId` | |
| `ShipmentId` | `ShipmentId` | |
| `TrackingNumber` | `string?` | |
| `ShippedAtUtc` | `DateTime` | |

---

#### `OrderDeliveredEvent`
| Field | Type | Description |
|-------|------|-------------|
| `OrderId` | `OrderId` | |
| `DeliveredAtUtc` | `DateTime` | |

---

#### `OrderCancelledEvent`
| Field | Type | Description |
|-------|------|-------------|
| `OrderId` | `OrderId` | |
| `Reason` | `string?` | |
| `CancelledAtUtc` | `DateTime` | |

---

#### `OrderRefundRequestedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `OrderId` | `OrderId` | |
| `RequestedAtUtc` | `DateTime` | |

---

### Payment Aggregate

#### `PaymentCapturedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `PaymentId` | `PaymentId` | |
| `OrderId` | `OrderId` | |
| `Amount` | `Money` | |
| `GatewayTransactionId` | `string` | |
| `CapturedAtUtc` | `DateTime` | |

---

#### `PaymentFailedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `PaymentId` | `PaymentId` | |
| `OrderId` | `OrderId` | |
| `FailureReason` | `string` | |

---

#### `PaymentRefundedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `PaymentId` | `PaymentId` | |
| `OrderId` | `OrderId` | |
| `RefundAmount` | `Money` | This refund's amount (not cumulative) |
| `TotalRefunded` | `Money` | Cumulative refunded so far |
| `RefundedAtUtc` | `DateTime` | |

---

### Shipment Aggregate

#### `ShipmentInTransitEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ShipmentId` | `ShipmentId` | |
| `OrderId` | `OrderId` | |
| `TrackingNumber` | `string` | |
| `CarrierCode` | `string` | |
| `ShippedAtUtc` | `DateTime` | |

---

#### `ShipmentDeliveredEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ShipmentId` | `ShipmentId` | |
| `OrderId` | `OrderId` | |
| `DeliveredAtUtc` | `DateTime` | |

---

### Promotion Aggregate

#### `PromotionExhaustedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `PromotionId` | `PromotionId` | |
| `Code` | `string` | Coupon code |
| `FinalUsageCount` | `int` | Equal to `MaxUsageCount` |
| `ExhaustedAtUtc` | `DateTime` | |

---

### ReturnRequest Aggregate

#### `ReturnRequestCreatedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ReturnRequestId` | `ReturnRequestId` | |
| `OrderId` | `OrderId` | |
| `CustomerId` | `CustomerId` | |
| `ItemCount` | `int` | Number of items in request |

---

#### `ReturnRequestApprovedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ReturnRequestId` | `ReturnRequestId` | |
| `ResolutionType` | `ResolutionType` | `Refund` or `Exchange` |
| `ApprovedAtUtc` | `DateTime` | |

---

#### `ReturnCompletedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ReturnRequestId` | `ReturnRequestId` | |
| `OrderId` | `OrderId` | |
| `CompletedAtUtc` | `DateTime` | |

---

#### `ExchangeCompletedEvent`
| Field | Type | Description |
|-------|------|-------------|
| `ReturnRequestId` | `ReturnRequestId` | |
| `OrderId` | `OrderId` | Original order |
| `ReplacementOrderId` | `OrderId?` | New replacement order (created by Application handler) |
| `CompletedAtUtc` | `DateTime` | |

---

### VendorSettings Aggregate

#### `VendorSettingsUpdatedEvent` *(from Feature 001)*
| Field | Type | Description |
|-------|------|-------------|
| `VendorSettingsId` | `VendorSettingsId` | |
| `UpdatedSection` | `string` | Which config section was patched |
| `UpdatedAtUtc` | `DateTime` | |

---

## Outbox Contract

All events are serialised to `OutboxMessages` via `System.Text.Json` by `Vendor.Infrastructure.Persistence.Interceptors.OutboxInterceptor` before `SaveChangesAsync` returns.

```sql
OutboxMessages (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,   -- = IDomainEvent.EventId
    OccurredOn      DATETIME2 NOT NULL,
    Type            NVARCHAR(500) NOT NULL,          -- full CLR type name
    Payload         NVARCHAR(MAX) NOT NULL,          -- JSON serialised event
    ProcessedOn     DATETIME2 NULL,
    Error           NVARCHAR(MAX) NULL
)
```

**Idempotency**: `OutboxProcessor` sets `ProcessedOn` atomically. Handlers must tolerate at-least-once delivery using `EventId` as the idempotency key.
