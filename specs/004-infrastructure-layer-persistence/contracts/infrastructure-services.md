# Contract: Infrastructure Services & Real-time Client Interfaces

**Feature**: 004-infrastructure-layer-persistence  

---

## 1. SignalR Admin Notification Hub (`/hubs/admin`)

Typed client interface `IAdminNotificationClient` exposed by `AdminNotificationHub`:

```csharp
public interface IAdminNotificationClient
{
    Task OnNewOrder(OrderDto order);
    Task OnPaymentReceived(PaymentDto payment);
    Task OnPaymentFailed(PaymentDto payment, string reason);
    Task OnLowStock(Guid productId, string sku, int remainingStock);
    Task OnOrderCancelled(Guid orderId, string reason);
    Task OnReturnRequested(ReturnRequestDto returnRequest);
    Task OnShipmentDelivered(Guid shipmentId, string trackingNumber);
    Task OnSettingsUpdated(VendorConfigDto config);
}
```

### SignalR Connection Options
- Endpoint: `/hubs/admin`
- Authentication: Query string parameter `?access_token=<JWT_TOKEN>`
- Redis Backplane (multi-instance): `AddSignalR().AddStackExchangeRedis(redisConnectionString)`

---

## 2. Infrastructure Service Implementations Matrix

| Domain / App Interface | Infrastructure Concrete Class | Package Dependency | Config Key |
|------------------------|-------------------------------|--------------------|------------|
| `IUnitOfWork` | `VendorDbContext` | `Microsoft.EntityFrameworkCore.SqlServer` | `ConnectionStrings:DefaultConnection` |
| `IIdempotencyStore` | `DbIdempotencyStore` | `Microsoft.EntityFrameworkCore.SqlServer` | `ConnectionStrings:DefaultConnection` |
| `ICacheService` | `InMemoryCacheService` / `RedisCacheService` | `Microsoft.Extensions.Caching.StackExchangeRedis` | `Caching:Provider` (`Memory`/`Redis`) |
| `ITokenService` | `JwtTokenService` | `System.IdentityModel.Tokens.Jwt` | `VendorBootConfig:Auth:JwtSecret` |
| `IExternalAuthService` | `ExternalAuthService` | `HttpClient` | `VendorBootConfig:Auth:GoogleClientId` / `FacebookAppId` |
| `IPaymentGateway` | `StripePaymentGateway` / `PayPalPaymentGateway` / `PaymobPaymentGateway` | `Stripe.net`, `HttpClient` | `VendorRuntimeConfig:Payments` |
| `IShippingProvider` | `FlatRateShippingProvider` / `ShippoShippingProvider` | `HttpClient` | `VendorRuntimeConfig:Shipping` |
| `INotificationSender` | `SendGridEmailSender` / `SmtpEmailSender` | `SendGrid`, `MailKit` | `Email:Provider` (`SendGrid`/`Smtp`) |
| `IRealtimeNotifier` | `SignalRRealtimeNotifier` | `Microsoft.AspNetCore.SignalR` | SignalR Hub `/hubs/admin` |
