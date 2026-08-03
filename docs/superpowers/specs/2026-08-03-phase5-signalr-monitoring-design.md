# Phase 5 — Real-time SignalR Monitoring & Admin WebSockets Hub Design Document

**Feature**: Phase 5 Real-time SignalR Monitoring & Admin WebSockets Hub  
**Date**: 2026-08-03  
**Status**: APPROVED  

---

## 1. Overview

Phase 5 completes the real-time administrative monitoring system for the vendor e-commerce platform. It expands `IRealtimeNotifier` and `SignalRRealtimeNotifier` to handle all 8 typed client events on `IAdminNotificationClient`, configures JWT query-string authentication for WebSocket handshakes at `/hubs/admin`, enforces `[Authorize(Roles = "Admin")]` on `AdminNotificationHub`, and adds optional Redis scale-out backplane support.

---

## 2. Component Architecture & Design

### 2.1 Expanded `IRealtimeNotifier` & `SignalRRealtimeNotifier`

File: `src/Vendor.Infrastructure/Realtime/AdminNotificationHub.cs`

1. **`IRealtimeNotifier` Interface**:
   ```csharp
   public interface IRealtimeNotifier
   {
       Task NotifyNewOrderAsync(OrderDto order, CancellationToken ct = default);
       Task NotifyPaymentReceivedAsync(PaymentDto payment, CancellationToken ct = default);
       Task NotifyPaymentFailedAsync(PaymentDto payment, string reason, CancellationToken ct = default);
       Task NotifyLowStockAsync(Guid productId, string sku, int remainingStock, CancellationToken ct = default);
       Task NotifyOrderCancelledAsync(Guid orderId, string reason, CancellationToken ct = default);
       Task NotifyReturnRequestedAsync(ReturnRequestDto returnRequest, CancellationToken ct = default);
       Task NotifyShipmentDeliveredAsync(Guid shipmentId, string trackingNumber, CancellationToken ct = default);
       Task NotifySettingsUpdatedAsync(VendorConfigDto config, CancellationToken ct = default);
   }
   ```

2. **`AdminNotificationHub`**:
   Annotated with `[Authorize(Roles = "Admin")]` to guard WebSocket connections against unauthorized access.

3. **`SignalRRealtimeNotifier`**:
   Implements all 8 methods delegating to `IHubContext<AdminNotificationHub, IAdminNotificationClient>.Clients.All`.

---

### 2.2 JWT Authentication for WebSockets Handshake

In browser WebSocket connections, custom `Authorization` HTTP headers cannot be set during the initial HTTP upgrade request. SignalR passes the token via query string `?access_token=<jwt>`.

In `ServiceExtensions.cs` / `DependencyInjection.cs`, `AddJwtBearer` is configured with `OnMessageReceived`:

```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/admin"))
        {
            context.Token = accessToken;
        }
        return Task.CompletedTask;
    }
};
```

---

### 2.3 Redis SignalR Backplane Scale-Out

In `DependencyInjection.cs`:
```csharp
var signalrBuilder = services.AddSignalR();
var redisConnectionString = configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    try
    {
        signalrBuilder.AddStackExchangeRedis(redisConnectionString);
    }
    catch
    {
        // Fall back to in-memory SignalR backplane for single-node development
    }
}
```

---

## 3. Verification & Testing Criteria

1. **Unit Testing**: `SignalRRealtimeNotifierTests` verifies all 8 notification methods dispatch correctly to `IHubContext`.
2. **SignalR Dependency Injection**: `IRealtimeNotifier` registered as Scoped/Singleton in `DependencyInjection.cs`.
3. **Solution Integrity**: All 234+ unit & integration tests passing.
