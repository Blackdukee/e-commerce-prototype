# Data Model: Core Domain Layer Aggregates

**Feature**: 002-domain-layer-aggregates
**Date**: 2026-07-25

> All aggregates live in `src/Vendor.Domain/`. Zero external NuGet dependencies.
> Value objects (`Money`, `Address`, `DateRange`, `Slug`, `Weight`, `Dimensions`) are
> mapped as EF Core **owned types** — they become columns on the owning aggregate's table,
> never separate tables. EF Core configuration lives in `Vendor.Infrastructure`.

---

## Shared Abstractions

### `IDomainEvent` (interface)
| Field | Type | Notes |
|-------|------|-------|
| `EventId` | `Guid` | Unique per event instance |
| `OccurredOnUtc` | `DateTime` | UTC timestamp, set at construction |

### `Entity<TId>` (abstract class)
| Field | Type | Notes |
|-------|------|-------|
| `Id` | `TId` | Strongly-typed ID (readonly record struct) |

### `AggregateRoot<TId>` : `Entity<TId>` (abstract class)
| Member | Type | Notes |
|--------|------|-------|
| `_domainEvents` | `List<IDomainEvent>` | Private; cleared by Infrastructure after outbox enqueue |
| `DomainEvents` | `IReadOnlyCollection<IDomainEvent>` | Public read-only view |
| `RaiseDomainEvent(e)` | `void` | Protected; called by aggregate methods |
| `ClearDomainEvents()` | `void` | Called by EF Core `SaveChangesAsync` override |

---

## Value Objects

### `Money` (readonly record struct)
| Field | Type | Validation |
|-------|------|-----------|
| `Amount` | `decimal` | Any (can be 0 for discounts) |
| `Currency` | `string` | Normalised to uppercase ISO-4217 (3 chars) |
| `Zero(string)` | static factory | Returns `new Money(0m, currency)` |
| `+(Money, Money)` | operator | Throws `CurrencyMismatchException` if currencies differ |
| `-(Money, Money)` | operator | Same guard |
| `*(Money, decimal)` | operator | Scalar multiplication; preserves currency |
| `/(Money, decimal)` | operator | Scalar division; throws `DivideByZeroException` if divisor = 0 |

EF owned-type columns (example on `Product`): `UnitPriceAmount DECIMAL(18,4)`, `UnitPriceCurrency NCHAR(3)`.

### `Address` (sealed record)
| Field | Type | Validation |
|-------|------|-----------|
| `Street` | `string` | Non-empty |
| `City` | `string` | Non-empty |
| `State` | `string` | Non-empty |
| `ZipCode` | `string` | Non-empty |
| `CountryCode` | `string` | ISO 3166-1 alpha-2 (2 chars), uppercased |

EF owned-type columns: flat columns on owning entity table.

### `DateRange` (readonly record struct)
| Field | Type | Validation |
|-------|------|-----------|
| `StartUtc` | `DateTime` | UTC |
| `EndUtc` | `DateTime` | `EndUtc >= StartUtc` enforced in constructor |
| `Contains(DateTime utcNow)` | bool | `StartUtc <= utcNow <= EndUtc` |
| `IsActive(DateTime utcNow)` | bool | Alias for `Contains` |

### `Slug` (readonly record struct)
| Field | Type | Validation |
|-------|------|-----------|
| `Value` | `string` | Pattern `^[a-z0-9\-]+$` enforced via `Regex`; throws `ArgumentException` on violation |

### `Weight` (readonly record struct)
| Field | Type | Validation |
|-------|------|-----------|
| `Value` | `decimal` | > 0 |
| `Unit` | `WeightUnit` enum | `Kg`, `Lb` |

### `Dimensions` (readonly record struct)
| Field | Type | Validation |
|-------|------|-----------|
| `Length` | `decimal` | > 0 |
| `Width` | `decimal` | > 0 |
| `Height` | `decimal` | > 0 |
| `Unit` | `DimensionUnit` enum | `Cm`, `In` |

---

## Aggregate 1 — `Product`

**ID**: `ProductId` (`readonly record struct`)  
**Table**: `Products`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `ProductId` | PK |
| `Name` | `string` | Non-empty |
| `Description` | `string` | Optional |
| `Slug` | `Slug` | Unique per product; owned type → `Slug_Value` column |
| `BasePrice` | `Money` | > 0 to activate; owned type |
| `Status` | `ProductStatus` enum | `Draft`, `Active`, `Discontinued` |
| `LowStockThreshold` | `int` | > 0 (configurable per product) |
| `Variants` | `List<ProductVariant>` | Navigation; at least 1 required before activation |
| `Images` | `List<ProductImage>` | Navigation; at least 1 required before activation |
| `CreatedAtUtc` | `DateTime` | Set at creation |

**Business methods**:
- `Activate()` — Guards: `BasePrice.Amount > 0`, `Images.Count >= 1`. Raises `ProductActivatedEvent`.
- `Deactivate()` — Raises `ProductDeactivatedEvent`.
- `AddVariant(ProductVariant)` — Guards: SKU unique within product.
- `RemoveVariant(ProductVariantId)` — Guards: at least 1 variant must remain after removal.

**Domain events raised**: `ProductActivatedEvent`, `ProductDeactivatedEvent`, `ProductLowStockEvent`.

### `ProductVariant` (Entity)

**ID**: `ProductVariantId`  
**Table**: `ProductVariants` (FK → `Products.Id`)

| Field | Type | Validation |
|-------|------|-----------|
| `Id` | `ProductVariantId` | PK |
| `ProductId` | `ProductId` | FK |
| `Sku` | `string` | Unique within `ProductId` |
| `PriceAdjustment` | `Money` | Additive to `Product.BasePrice`; 0 = same as base |
| `StockQuantity` | `int` | >= 0 (invariant enforced on deduction) |
| `Weight` | `Weight` | Owned type |
| `Dimensions` | `Dimensions` | Owned type |

**Business methods**:
- `DeductStock(int qty, int lowStockThreshold)` — Guards: `StockQuantity - qty >= 0`. Raises `ProductLowStockEvent` when result < threshold.
- `AddStock(int qty)` — Guards: `qty > 0`.

---

## Aggregate 2 — `Customer`

**ID**: `CustomerId`  
**Table**: `Customers`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `CustomerId` | PK |
| `Email` | `string` | Non-empty; unique enforced at repository level |
| `FirstName` | `string` | Non-empty |
| `LastName` | `string` | Non-empty |
| `CustomerType` | `CustomerType` enum | `Guest`, `Registered` |
| `AnalyticsConsent` | `bool` | Default `false` |
| `ConsentUpdatedAtUtc` | `DateTime?` | Set when consent toggled |
| `RegisteredAtUtc` | `DateTime?` | Set on guest-to-registered conversion |
| `ShippingAddresses` | `List<Address>` | Owned type collection |
| `CreatedAtUtc` | `DateTime` | |

**Business methods**:
- `ConvertToRegistered(string email)` — Guards: `CustomerType == Guest`; one-way; sets `RegisteredAtUtc`. Raises `CustomerCreatedEvent`.
- `UpdateConsent(bool granted)` — Sets `AnalyticsConsent`, `ConsentUpdatedAtUtc`. Raises `CustomerConsentUpdatedEvent`.

**Domain events raised**: `CustomerCreatedEvent`, `CustomerConsentUpdatedEvent`.

---

## Aggregate 3 — `Cart`

**ID**: `CartId`  
**Table**: `Carts`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `CartId` | PK |
| `CustomerId` | `CustomerId?` | Null for guest carts |
| `SessionId` | `string?` | For guest carts |
| `Status` | `CartStatus` enum | `Active`, `Merged`, `ConvertedToOrder`, `Abandoned` |
| `DiscountCode` | `string?` | Single code; replaced (not stacked) on reapply |
| `Items` | `List<CartItem>` | Navigation |
| `LastModifiedUtc` | `DateTime` | Updated on any mutation |
| `CreatedAtUtc` | `DateTime` | |

**Business methods**:
- `AddItem(CartItem item, int maxItems)` — Guards: `Items.Count < maxItems`. Updates `LastModifiedUtc`.
- `RemoveItem(ProductVariantId variantId)` — Guards: item must exist.
- `ApplyDiscount(string code)` — Replaces existing; sets `DiscountCode`.
- `RemoveDiscount()` — Clears `DiscountCode`.
- `Merge(Cart guestCart)` — Guards: this cart is for a registered customer; guest cart is `Active`. Copies items; marks `guestCart.Status = Merged`.
- `IsAbandoned(DateTime utcNow, TimeSpan timeout)` — Pure predicate; no side effects.
- `MarkAbandoned(DateTime utcNow, TimeSpan timeout)` — Calls `IsAbandoned`; transitions to `Abandoned`. Raises `CartAbandonedEvent`.

### `CartItem` (Entity — no aggregate root)
| Field | Type | |
|-------|------|--|
| `CartId` | `CartId` | FK |
| `ProductVariantId` | `ProductVariantId` | |
| `Quantity` | `int` | > 0 |
| `UnitPrice` | `Money` | Snapshot at add time |

---

## Aggregate 4 — `Order`

**ID**: `OrderId`  
**Table**: `Orders`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `OrderId` | PK |
| `CustomerId` | `CustomerId` | FK |
| `Status` | `OrderStatus` enum | See state machine below |
| `OrderNumber` | `string` | `{prefix}-{YYYYMMDD}-{seq}` (prefix from VendorSettings) |
| `Lines` | `IReadOnlyList<OrderLine>` | Immutable after creation |
| `ShippingAddress` | `Address` | Owned type |
| `Subtotal` | `Money` | `sum(line.LineTotal)` |
| `Tax` | `Money` | From `ITaxCalculator` result |
| `ShippingCost` | `Money` | From `IShippingProvider` result |
| `Discount` | `Money` | From applied promotion |
| `Total` | `Money` | `Subtotal + Tax + ShippingCost - Discount` must be >= 0 |
| `PlacedAtUtc` | `DateTime` | |

**State machine** (`OrderStatus`):
```
Pending → [Confirmed, Cancelled]
Confirmed → [Processing, Cancelled, RefundRequested]
Processing → [Shipped, Cancelled, RefundRequested]
Shipped → [Delivered, ReturnRequested, ExchangeRequested]
Delivered → [ReturnRequested, ExchangeRequested]
RefundRequested → [Refunded]
ReturnRequested → [Returned]
ExchangeRequested → [Exchanged]
Cancelled | Refunded | Returned | Exchanged → [] (terminal)
```

**Business methods**: `ConfirmPayment()`, `StartProcessing()`, `Ship()`, `Deliver()`, `Cancel()`, `RequestRefund()`, `Refund()`, `RequestReturn()`, `CompleteReturn()`, `RequestExchange()`, `CompleteExchange()`.  
All call `EnsureCanTransitionTo(next)` → raise matching domain event.

**Invariant**: `Total.Amount >= 0` enforced in factory method / constructor.

**Domain events raised**: `OrderPlacedEvent`, `OrderConfirmedEvent`, `OrderShippedEvent`, `OrderDeliveredEvent`, `OrderCancelledEvent`, `OrderRefundRequestedEvent`.

### `OrderLine` (Entity — value-like, immutable)
| Field | Type | |
|-------|------|--|
| `OrderId` | `OrderId` | FK |
| `ProductVariantId` | `ProductVariantId` | Snapshot |
| `ProductName` | `string` | Snapshot at order time |
| `Sku` | `string` | Snapshot |
| `Quantity` | `int` | Immutable |
| `UnitPrice` | `Money` | Snapshot; owned type |
| `LineTotal` | `Money` | `UnitPrice * Quantity`; derived |

---

## Aggregate 5 — `Payment`

**ID**: `PaymentId`  
**Table**: `Payments`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `PaymentId` | PK |
| `OrderId` | `OrderId` | FK |
| `Status` | `PaymentStatus` enum | `Pending`, `Authorized`, `Captured`, `Failed`, `Refunded`, `PartiallyRefunded` |
| `Amount` | `Money` | Captured amount |
| `RefundedAmount` | `Money` | Cumulative; starts at `Money.Zero(currency)` |
| `IdempotencyKey` | `string` | Unique; prevents double-charge |
| `GatewayTransactionId` | `string?` | From payment gateway |
| `FailureReason` | `string?` | Set on failure |
| `CapturedAtUtc` | `DateTime?` | |

**Business methods**:
- `Capture(string transactionId, DateTime utcNow)` — Guards: `Status == Authorized`. Raises `PaymentCapturedEvent`.
- `Fail(string reason)` — Sets `Status = Failed`, `FailureReason`. Raises `PaymentFailedEvent`.
- `Refund(Money amount)` — Guards: `Status == Captured || PartiallyRefunded`; `RefundedAmount + amount <= Amount`. Updates `RefundedAmount`; transitions to `Refunded` or `PartiallyRefunded`. Raises `PaymentRefundedEvent`.

**Domain events raised**: `PaymentCapturedEvent`, `PaymentFailedEvent`, `PaymentRefundedEvent`.

---

## Aggregate 6 — `Shipment`

**ID**: `ShipmentId`  
**Table**: `Shipments`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `ShipmentId` | PK |
| `OrderId` | `OrderId` | FK |
| `Status` | `ShipmentStatus` enum | `Pending`, `LabelCreated`, `InTransit`, `OutForDelivery`, `Delivered`, `Failed` |
| `TrackingNumber` | `string?` | Set only when `Status == LabelCreated` |
| `CarrierCode` | `string` | e.g. "SHIPPO", "FLATRATE" |
| `ShippingAddress` | `Address` | Owned type |
| `EstimatedDeliveryUtc` | `DateTime?` | |
| `ShippedAtUtc` | `DateTime?` | |

**Linear status progression** (no back-transitions):
`Pending → LabelCreated → InTransit → OutForDelivery → Delivered`  
`InTransit | OutForDelivery → Failed` (terminal side branch)

**Business methods**:
- `CreateLabel(string trackingNumber, DateTime estimatedDelivery)` — Guards: `Status == Pending`. Sets `TrackingNumber`. Raises nothing (infrastructure only).
- `MarkInTransit(DateTime shippedAt)` — Guards: `Status == LabelCreated`. Raises `ShipmentInTransitEvent`.
- `MarkDelivered(DateTime deliveredAt)` — Guards: `Status == OutForDelivery`. Raises `ShipmentDeliveredEvent`.

**Domain events raised**: `ShipmentInTransitEvent`, `ShipmentDeliveredEvent`.

---

## Aggregate 7 — `Promotion`

**ID**: `PromotionId`  
**Table**: `Promotions`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `PromotionId` | PK |
| `Code` | `string` | Unique coupon code |
| `DiscountType` | `DiscountType` enum | `Percentage`, `Fixed` |
| `DiscountValue` | `decimal` | Percentage (0–100) or fixed `Money` amount |
| `MaxDiscountAmount` | `Money?` | Cap for percentage discounts |
| `MinOrderAmount` | `Money?` | Minimum qualifying order subtotal |
| `Validity` | `DateRange` | Owned type; promotion active only within range |
| `MaxUsageCount` | `int?` | Null = unlimited |
| `CurrentUsageCount` | `int` | Starts at 0 |
| `IsActive` | `bool` | Auto-set false at max usage |

**Business methods**:
- `Apply(Money orderSubtotal, DateTime utcNow)` → `Money discount` — Guards: `IsActive`, `Validity.Contains(utcNow)`, `orderSubtotal >= MinOrderAmount`. Increments `CurrentUsageCount`. If `CurrentUsageCount == MaxUsageCount`, deactivates and raises `PromotionExhaustedEvent`.
- `Deactivate()` — Sets `IsActive = false`.

**Domain events raised**: `PromotionExhaustedEvent`.

---

## Aggregate 8 — `ReturnRequest`

**ID**: `ReturnRequestId`  
**Table**: `ReturnRequests`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `ReturnRequestId` | PK |
| `OrderId` | `OrderId` | FK |
| `CustomerId` | `CustomerId` | FK |
| `Status` | `ReturnRequestStatus` enum | `Pending`, `Approved`, `Rejected`, `Returned`, `Exchanged` |
| `Reason` | `string` | Non-empty |
| `Items` | `List<ReturnItem>` | >= 1 required |
| `ResolutionType` | `ResolutionType?` enum | `Refund`, `Exchange` (set at approval) |
| `CreatedAtUtc` | `DateTime` | |

**Business methods**:
- `Approve(ResolutionType resolution)` — Guards: `Status == Pending`. Raises `ReturnRequestApprovedEvent`.
- `CompleteReturn()` — Guards: `Status == Approved && ResolutionType == Refund`. Transitions to `Returned`. Raises `ReturnCompletedEvent`.
- `CompleteExchange()` — Guards: `Status == Approved && ResolutionType == Exchange`. Transitions to `Exchanged`. Raises `ExchangeCompletedEvent`.
- `Reject()` — Guards: `Status == Pending`. Transitions to `Rejected`.

**Domain events raised**: `ReturnRequestCreatedEvent`, `ReturnRequestApprovedEvent`, `ReturnCompletedEvent`, `ExchangeCompletedEvent`.

### `ReturnItem` (value object / entity)
| Field | Type | |
|-------|------|--|
| `OrderLineId` | `Guid` | Reference to original order line |
| `ProductVariantId` | `ProductVariantId` | |
| `Quantity` | `int` | > 0 |
| `Reason` | `string` | |

---

## Aggregate 9 — `VendorSettings`

**ID**: `VendorSettingsId`  
**Table**: `VendorSettings` (already defined in Feature 001)

*(This feature references the existing aggregate from Feature 001. No structural changes are needed. `VendorSettingsUpdatedEvent` is already defined.)*

**Domain events raised**: `VendorSettingsUpdatedEvent` (Feature 001).

---

## Aggregate 10 — `AnalyticsEvent`

**ID**: `AnalyticsEventId`  
**Table**: `AnalyticsEvents`

| Field | Type | Validation / Notes |
|-------|------|-------------------|
| `Id` | `AnalyticsEventId` | PK |
| `CustomerId` | `CustomerId?` | Null for anonymous events |
| `EventType` | `string` | e.g. "ProductViewed", "CartAbandoned" |
| `Payload` | `string` | JSON snapshot (immutable) |
| `ConsentGrantedAtCapture` | `bool` | Snapshot of `Customer.AnalyticsConsent` at event time |
| `OccurredAtUtc` | `DateTime` | Immutable; set at construction |

**Immutability**: No business methods mutate the aggregate after construction. Created via a static `Capture(...)` factory method that validates `EventType` is non-empty and returns a new immutable instance.

---

## Domain Event Catalogue

| # | Event | Aggregate | Trigger |
|---|-------|-----------|---------|
| 1 | `ProductActivatedEvent` | Product | `Activate()` succeeds |
| 2 | `ProductDeactivatedEvent` | Product | `Deactivate()` |
| 3 | `ProductLowStockEvent` | ProductVariant | `DeductStock()` result < threshold |
| 4 | `CustomerCreatedEvent` | Customer | `ConvertToRegistered()` |
| 5 | `CustomerConsentUpdatedEvent` | Customer | `UpdateConsent()` |
| 6 | `CartAbandonedEvent` | Cart | `MarkAbandoned()` |
| 7 | `OrderPlacedEvent` | Order | Constructor (factory) |
| 8 | `OrderConfirmedEvent` | Order | `ConfirmPayment()` |
| 9 | `OrderShippedEvent` | Order | `Ship()` |
| 10 | `OrderDeliveredEvent` | Order | `Deliver()` |
| 11 | `OrderCancelledEvent` | Order | `Cancel()` |
| 12 | `OrderRefundRequestedEvent` | Order | `RequestRefund()` |
| 13 | `PaymentCapturedEvent` | Payment | `Capture()` |
| 14 | `PaymentFailedEvent` | Payment | `Fail()` |
| 15 | `PaymentRefundedEvent` | Payment | `Refund()` |
| 16 | `ShipmentInTransitEvent` | Shipment | `MarkInTransit()` |
| 17 | `ShipmentDeliveredEvent` | Shipment | `MarkDelivered()` |
| 18 | `PromotionExhaustedEvent` | Promotion | `Apply()` hits max usage |
| 19 | `ReturnRequestCreatedEvent` | ReturnRequest | Constructor |
| 20 | `ReturnRequestApprovedEvent` | ReturnRequest | `Approve()` |
| 21 | `ReturnCompletedEvent` | ReturnRequest | `CompleteReturn()` |
| 22 | `ExchangeCompletedEvent` | ReturnRequest | `CompleteExchange()` |

---

## Domain Exceptions

| Exception | Trigger |
|-----------|---------|
| `DomainException` | Base class for all domain exceptions |
| `CurrencyMismatchException` | Cross-currency `Money` arithmetic |
| `InvalidStateTransitionException` | Illegal state machine transition (Order, Payment, Shipment, ReturnRequest) |
| `BusinessRuleViolationException` | General invariant violation (e.g., negative stock, price <= 0) |

---

## Repository Interfaces (Domain)

All interfaces use `CancellationToken` on async methods and return domain aggregates or `null`.

| Interface | Key Methods |
|-----------|------------|
| `IProductRepository` | `GetByIdAsync`, `GetBySlugAsync`, `AddAsync`, `UpdateAsync` |
| `ICustomerRepository` | `GetByIdAsync`, `GetByEmailAsync`, `AddAsync`, `UpdateAsync` |
| `ICartRepository` | `GetByIdAsync`, `GetByCustomerIdAsync`, `GetBySessionIdAsync`, `AddAsync`, `UpdateAsync` |
| `IOrderRepository` | `GetByIdAsync`, `GetByOrderNumberAsync`, `AddAsync`, `UpdateAsync` |
| `IPaymentRepository` | `GetByIdAsync`, `GetByIdempotencyKeyAsync`, `AddAsync`, `UpdateAsync` |
| `IShipmentRepository` | `GetByIdAsync`, `GetByOrderIdAsync`, `AddAsync`, `UpdateAsync` |
| `IPromotionRepository` | `GetByIdAsync`, `GetByCodeAsync`, `AddAsync`, `UpdateAsync` |
| `IReturnRequestRepository` | `GetByIdAsync`, `GetByOrderIdAsync`, `AddAsync`, `UpdateAsync` |
| `IVendorSettingsRepository` | `GetAsync`, `UpdateAsync` (Feature 001; already exists) |
| `IAnalyticsEventRepository` | `AddAsync`, `GetByCustomerIdAsync` |

## Adapter Interfaces (Domain)

| Interface | Responsibility |
|-----------|---------------|
| `IPaymentGateway` | `AuthorizeAsync`, `CaptureAsync`, `RefundAsync` |
| `IShippingProvider` | `GetRatesAsync`, `CreateLabelAsync`, `TrackShipmentAsync` |
| `ITaxCalculator` | `CalculateTaxAsync(order, address) → Money` |
| `IAnalyticsForwarder` | `ForwardAsync(AnalyticsEvent)` |
| `INotificationSender` | `SendOrderConfirmationAsync`, `SendShipmentNotificationAsync` |
| `ISecretResolver` | `ResolveAsync(string reference) → string` (Feature 001; already exists) |
