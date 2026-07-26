# Contract: Repository & Adapter Interfaces

**Feature**: 002-domain-layer-aggregates
**Scope**: All interfaces defined in `Vendor.Domain` — no implementations here.
**Package**: Zero external NuGet references; BCL types (`Task<T>`, `CancellationToken`) only.

---

## Conventions

1. All async methods accept a `CancellationToken cancellationToken = default` as the last parameter.
2. Methods that may return nothing return `Task<TAgg?>` (nullable). Callers handle `null` = not found.
3. Repository interfaces follow the **Repository** pattern — single aggregate per interface.
4. Adapter interfaces follow the **Port** pattern — the Domain declares what it needs; Infrastructure provides the plug.

---

## Repository Interfaces

### `IProductRepository`
```csharp
public interface IProductRepository
{
    Task<Product?>  GetByIdAsync(ProductId id,      CancellationToken ct = default);
    Task<Product?>  GetBySlugAsync(Slug slug,        CancellationToken ct = default);
    Task            AddAsync(Product product,         CancellationToken ct = default);
    Task            UpdateAsync(Product product,      CancellationToken ct = default);
    Task<bool>      ExistsAsync(ProductId id,         CancellationToken ct = default);
}
```

### `ICustomerRepository`
```csharp
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id,      CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email,    CancellationToken ct = default);
    Task            AddAsync(Customer customer,       CancellationToken ct = default);
    Task            UpdateAsync(Customer customer,    CancellationToken ct = default);
    Task<bool>      EmailExistsAsync(string email,   CancellationToken ct = default);
}
```

### `ICartRepository`
```csharp
public interface ICartRepository
{
    Task<Cart?>  GetByIdAsync(CartId id,                         CancellationToken ct = default);
    Task<Cart?>  GetByCustomerIdAsync(CustomerId customerId,     CancellationToken ct = default);
    Task<Cart?>  GetBySessionIdAsync(string sessionId,           CancellationToken ct = default);
    Task         AddAsync(Cart cart,                             CancellationToken ct = default);
    Task         UpdateAsync(Cart cart,                          CancellationToken ct = default);
    Task<IReadOnlyList<Cart>> GetAbandonedCartsAsync(
        DateTime abandonedBefore, CancellationToken ct = default);
}
```

### `IOrderRepository`
```csharp
public interface IOrderRepository
{
    Task<Order?>  GetByIdAsync(OrderId id,               CancellationToken ct = default);
    Task<Order?>  GetByOrderNumberAsync(string number,   CancellationToken ct = default);
    Task          AddAsync(Order order,                  CancellationToken ct = default);
    Task          UpdateAsync(Order order,               CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        CustomerId customerId, CancellationToken ct = default);
}
```

### `IPaymentRepository`
```csharp
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId id,                     CancellationToken ct = default);
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<Payment?> GetByOrderIdAsync(OrderId orderId,             CancellationToken ct = default);
    Task           AddAsync(Payment payment,                       CancellationToken ct = default);
    Task           UpdateAsync(Payment payment,                    CancellationToken ct = default);
}
```

### `IShipmentRepository`
```csharp
public interface IShipmentRepository
{
    Task<Shipment?>  GetByIdAsync(ShipmentId id,       CancellationToken ct = default);
    Task<Shipment?>  GetByOrderIdAsync(OrderId orderId, CancellationToken ct = default);
    Task             AddAsync(Shipment shipment,        CancellationToken ct = default);
    Task             UpdateAsync(Shipment shipment,     CancellationToken ct = default);
}
```

### `IPromotionRepository`
```csharp
public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(PromotionId id,   CancellationToken ct = default);
    Task<Promotion?> GetByCodeAsync(string code,    CancellationToken ct = default);
    Task             AddAsync(Promotion promotion,  CancellationToken ct = default);
    Task             UpdateAsync(Promotion promotion, CancellationToken ct = default);
}
```

### `IReturnRequestRepository`
```csharp
public interface IReturnRequestRepository
{
    Task<ReturnRequest?>  GetByIdAsync(ReturnRequestId id, CancellationToken ct = default);
    Task<IReadOnlyList<ReturnRequest>> GetByOrderIdAsync(
        OrderId orderId, CancellationToken ct = default);
    Task                  AddAsync(ReturnRequest request,  CancellationToken ct = default);
    Task                  UpdateAsync(ReturnRequest request, CancellationToken ct = default);
}
```

### `IVendorSettingsRepository` *(Feature 001 — already implemented)*
```csharp
// Defined in Vendor.Domain/Interfaces/IVendorSettingsRepository.cs
// No changes required for this feature.
```

### `IAnalyticsEventRepository`
```csharp
public interface IAnalyticsEventRepository
{
    Task AddAsync(AnalyticsEvent analyticsEvent, CancellationToken ct = default);
    Task<IReadOnlyList<AnalyticsEvent>> GetByCustomerIdAsync(
        CustomerId customerId,
        int pageSize       = 50,
        int pageIndex      = 0,
        CancellationToken ct = default);
}
```

---

## Adapter Interfaces (Ports)

### `IPaymentGateway`
```csharp
public interface IPaymentGateway
{
    Task<PaymentAuthorizationResult> AuthorizeAsync(
        Money amount,
        string idempotencyKey,
        PaymentMethodDetails method,
        CancellationToken ct = default);

    Task<PaymentCaptureResult> CaptureAsync(
        string authorizationToken,
        string idempotencyKey,
        CancellationToken ct = default);

    Task<PaymentRefundResult> RefundAsync(
        string gatewayTransactionId,
        Money refundAmount,
        string idempotencyKey,
        CancellationToken ct = default);
}
```

*Return types (`PaymentAuthorizationResult`, `PaymentCaptureResult`, `PaymentRefundResult`) are value records defined in Domain with no external dependencies.*

---

### `IShippingProvider`
```csharp
public interface IShippingProvider
{
    Task<IReadOnlyList<ShippingRate>> GetRatesAsync(
        Address origin,
        Address destination,
        Weight weight,
        Dimensions dimensions,
        CancellationToken ct = default);

    Task<ShippingLabel> CreateLabelAsync(
        ShippingRate selectedRate,
        Address origin,
        Address destination,
        CancellationToken ct = default);

    Task<ShipmentTrackingInfo> TrackShipmentAsync(
        string trackingNumber,
        string carrierCode,
        CancellationToken ct = default);
}
```

---

### `ITaxCalculator`
```csharp
public interface ITaxCalculator
{
    Task<Money> CalculateTaxAsync(
        IReadOnlyList<OrderLine> lines,
        Address shippingAddress,
        string currencyCode,
        CancellationToken ct = default);
}
```

---

### `IAnalyticsForwarder`
```csharp
public interface IAnalyticsForwarder
{
    Task ForwardAsync(AnalyticsEvent analyticsEvent, CancellationToken ct = default);
}
```

---

### `INotificationSender`
```csharp
public interface INotificationSender
{
    Task SendOrderConfirmationAsync(
        CustomerId customerId,
        OrderId orderId,
        string orderNumber,
        CancellationToken ct = default);

    Task SendShipmentNotificationAsync(
        CustomerId customerId,
        OrderId orderId,
        string trackingNumber,
        string carrierCode,
        CancellationToken ct = default);

    Task SendReturnConfirmationAsync(
        CustomerId customerId,
        ReturnRequestId returnRequestId,
        CancellationToken ct = default);
}
```

---

### `ISecretResolver` *(Feature 001 — already implemented)*
```csharp
// Defined in Vendor.Domain/Interfaces/ISecretResolver.cs
// No changes required for this feature.
```

---

## Dependency Map

```text
Vendor.Domain (this feature)
  └── Defines: AggregateRoot<TId>, IDomainEvent, all VOs, all aggregates
  └── Declares: IProductRepository, ICustomerRepository, ICartRepository,
                IOrderRepository, IPaymentRepository, IShipmentRepository,
                IPromotionRepository, IReturnRequestRepository,
                IVendorSettingsRepository, IAnalyticsEventRepository,
                IPaymentGateway, IShippingProvider, ITaxCalculator,
                IAnalyticsForwarder, INotificationSender, ISecretResolver

Vendor.Infrastructure (future feature)
  └── Implements: all IXxxRepository → EF Core + MSSQL
  └── Implements: all IXxxAdapter  → Stripe/Shippo/SendGrid/etc.

Vendor.Application (future feature)
  └── Injects: interfaces via constructor DI
  └── Never imports: Infrastructure types directly
```
