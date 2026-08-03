# Phase 5 — Real-time SignalR Monitoring & Admin WebSockets Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement full 8-event real-time SignalR notification broadcasting for admin monitoring, configure WebSocket JWT query-string authentication, and add optional Redis backplane scale-out.

**Architecture:** `AdminNotificationHub` (`/hubs/admin`) implementing `IAdminNotificationClient`, `SignalRRealtimeNotifier` implementing expanded `IRealtimeNotifier`, query string token parsing in `JwtBearerEvents.OnMessageReceived`, and DI registration.

**Tech Stack:** ASP.NET Core SignalR, `Microsoft.AspNetCore.Authentication.JwtBearer`, StackExchange.Redis (backplane), Moq, FluentAssertions, xUnit.

## Global Constraints

- `AdminNotificationHub` MUST be guarded with `[Authorize(Roles = "Admin")]`.
- `IRealtimeNotifier` MUST support all 8 methods: `NotifyNewOrderAsync`, `NotifyPaymentReceivedAsync`, `NotifyPaymentFailedAsync`, `NotifyLowStockAsync`, `NotifyOrderCancelledAsync`, `NotifyReturnRequestedAsync`, `NotifyShipmentDeliveredAsync`, `NotifySettingsUpdatedAsync`.
- `OnMessageReceived` MUST parse `access_token` from query string when `path.StartsWithSegments("/hubs/admin")`.
- `IRealtimeNotifier` MUST be registered in `DependencyInjection.cs`.

---

### Task 1: Expand `IRealtimeNotifier`, `AdminNotificationHub`, and `SignalRRealtimeNotifier`

**Files:**
- Modify: `src/Vendor.Infrastructure/Realtime/AdminNotificationHub.cs`

**Interfaces:**
- Consumes: `IHubContext<AdminNotificationHub, IAdminNotificationClient>`
- Produces: `IRealtimeNotifier` with 8 notification dispatch methods

- [ ] **Step 1: Update `AdminNotificationHub.cs`**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Vendor.Application.Modules.Auth;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Application.Modules.Payments;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Application.Modules.VendorSettings;

namespace Vendor.Infrastructure.Realtime;

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

[Authorize(Roles = "Admin")]
public class AdminNotificationHub : Hub<IAdminNotificationClient>;

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

public class SignalRRealtimeNotifier(IHubContext<AdminNotificationHub, IAdminNotificationClient> hubContext) : IRealtimeNotifier
{
    public Task NotifyNewOrderAsync(OrderDto order, CancellationToken ct = default)
        => hubContext.Clients.All.OnNewOrder(order);

    public Task NotifyPaymentReceivedAsync(PaymentDto payment, CancellationToken ct = default)
        => hubContext.Clients.All.OnPaymentReceived(payment);

    public Task NotifyPaymentFailedAsync(PaymentDto payment, string reason, CancellationToken ct = default)
        => hubContext.Clients.All.OnPaymentFailed(payment, reason);

    public Task NotifyLowStockAsync(Guid productId, string sku, int remainingStock, CancellationToken ct = default)
        => hubContext.Clients.All.OnLowStock(productId, sku, remainingStock);

    public Task NotifyOrderCancelledAsync(Guid orderId, string reason, CancellationToken ct = default)
        => hubContext.Clients.All.OnOrderCancelled(orderId, reason);

    public Task NotifyReturnRequestedAsync(ReturnRequestDto returnRequest, CancellationToken ct = default)
        => hubContext.Clients.All.OnReturnRequested(returnRequest);

    public Task NotifyShipmentDeliveredAsync(Guid shipmentId, string trackingNumber, CancellationToken ct = default)
        => hubContext.Clients.All.OnShipmentDelivered(shipmentId, trackingNumber);

    public Task NotifySettingsUpdatedAsync(VendorConfigDto config, CancellationToken ct = default)
        => hubContext.Clients.All.OnSettingsUpdated(config);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Vendor.Infrastructure/Realtime/AdminNotificationHub.cs
git commit -m "feat(realtime): expand IRealtimeNotifier with 8 events and authorize AdminNotificationHub"
```

---

### Task 2: Configure JWT Query String Handshake Auth & SignalR Services in DI

**Files:**
- Modify: `src/Vendor.Infrastructure/DependencyInjection.cs`
- Modify: `src/Vendor.Api/Extensions/ServiceExtensions.cs`

**Interfaces:**
- Consumes: `IServiceCollection`, `IConfiguration`
- Produces: Registered `IRealtimeNotifier`, SignalR services with optional Redis backplane, and JWT WebSocket handshake token parsing

- [ ] **Step 1: Register `IRealtimeNotifier` and SignalR in `DependencyInjection.cs`**

Add to `DependencyInjection.cs`:
```csharp
services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

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
        // Fall back to in-memory SignalR backplane
    }
}
```

- [ ] **Step 2: Update `ServiceExtensions.cs` for WebSocket Query String JWT Authentication**

In `src/Vendor.Api/Extensions/ServiceExtensions.cs`, update `AddJwtBearer` configuration:

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

- [ ] **Step 3: Commit**

```bash
git add src/Vendor.Infrastructure/DependencyInjection.cs src/Vendor.Api/Extensions/ServiceExtensions.cs
git commit -m "feat(realtime): register IRealtimeNotifier and configure JWT query string auth for SignalR"
```

---

### Task 3: Unit Tests & Verification Audit

**Files:**
- Create: `tests/Vendor.Infrastructure.Tests/Realtime/SignalRRealtimeNotifierTests.cs`

**Interfaces:**
- Consumes: `SignalRRealtimeNotifier`, `IHubContext<AdminNotificationHub, IAdminNotificationClient>`
- Produces: Verified unit tests for all 8 notification methods

- [ ] **Step 1: Create `SignalRRealtimeNotifierTests.cs`**

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Vendor.Application.Modules.Auth;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Application.Modules.Payments;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Application.Modules.VendorSettings;
using Vendor.Infrastructure.Realtime;
using Xunit;

namespace Vendor.Infrastructure.Tests.Realtime;

public class SignalRRealtimeNotifierTests
{
    private readonly Mock<IHubContext<AdminNotificationHub, IAdminNotificationClient>> _mockHubContext;
    private readonly Mock<IHubClients<IAdminNotificationClient>> _mockClients;
    private readonly Mock<IAdminNotificationClient> _mockAdminClient;
    private readonly SignalRRealtimeNotifier _notifier;

    public SignalRRealtimeNotifierTests()
    {
        _mockHubContext = new Mock<IHubContext<AdminNotificationHub, IAdminNotificationClient>>();
        _mockClients = new Mock<IHubClients<IAdminNotificationClient>>();
        _mockAdminClient = new Mock<IAdminNotificationClient>();

        _mockClients.Setup(c => c.All).Returns(_mockAdminClient.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);

        _notifier = new SignalRRealtimeNotifier(_mockHubContext.Object);
    }

    [Fact]
    public async Task NotifyNewOrderAsync_DispatchesOnNewOrder()
    {
        var dto = new OrderDto(Guid.NewGuid(), "ORD-001", "Customer", 99.99m, "USD", "Pending", DateTime.UtcNow);
        await _notifier.NotifyNewOrderAsync(dto);
        _mockAdminClient.Verify(c => c.OnNewOrder(dto), Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentReceivedAsync_DispatchesOnPaymentReceived()
    {
        var dto = new PaymentDto(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD", "Completed", "Stripe", DateTime.UtcNow);
        await _notifier.NotifyPaymentReceivedAsync(dto);
        _mockAdminClient.Verify(c => c.OnPaymentReceived(dto), Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentFailedAsync_DispatchesOnPaymentFailed()
    {
        var dto = new PaymentDto(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD", "Failed", "Stripe", DateTime.UtcNow);
        await _notifier.NotifyPaymentFailedAsync(dto, "Card declined");
        _mockAdminClient.Verify(c => c.OnPaymentFailed(dto, "Card declined"), Times.Once);
    }

    [Fact]
    public async Task NotifyLowStockAsync_DispatchesOnLowStock()
    {
        var productId = Guid.NewGuid();
        await _notifier.NotifyLowStockAsync(productId, "SKU-123", 2);
        _mockAdminClient.Verify(c => c.OnLowStock(productId, "SKU-123", 2), Times.Once);
    }

    [Fact]
    public async Task NotifyOrderCancelledAsync_DispatchesOnOrderCancelled()
    {
        var orderId = Guid.NewGuid();
        await _notifier.NotifyOrderCancelledAsync(orderId, "User requested");
        _mockAdminClient.Verify(c => c.OnOrderCancelled(orderId, "User requested"), Times.Once);
    }

    [Fact]
    public async Task NotifyReturnRequestedAsync_DispatchesOnReturnRequested()
    {
        var dto = new ReturnRequestDto(Guid.NewGuid(), Guid.NewGuid(), "Defective", "Pending", DateTime.UtcNow);
        await _notifier.NotifyReturnRequestedAsync(dto);
        _mockAdminClient.Verify(c => c.OnReturnRequested(dto), Times.Once);
    }

    [Fact]
    public async Task NotifyShipmentDeliveredAsync_DispatchesOnShipmentDelivered()
    {
        var shipmentId = Guid.NewGuid();
        await _notifier.NotifyShipmentDeliveredAsync(shipmentId, "TRACK-999");
        _mockAdminClient.Verify(c => c.OnShipmentDelivered(shipmentId, "TRACK-999"), Times.Once);
    }

    [Fact]
    public async Task NotifySettingsUpdatedAsync_DispatchesOnSettingsUpdated()
    {
        var config = new VendorConfigDto("acme", "ACME Store");
        await _notifier.NotifySettingsUpdatedAsync(config);
        _mockAdminClient.Verify(c => c.OnSettingsUpdated(config), Times.Once);
    }
}
```

- [ ] **Step 2: Execute solution build & test suite**

Run: `dotnet test Vendor.slnx --logger "console;verbosity=normal"`
Expected: All unit & integration tests pass cleanly.

- [ ] **Step 3: Commit**

```bash
git add tests/Vendor.Infrastructure.Tests/Realtime/SignalRRealtimeNotifierTests.cs
git commit -m "test(realtime): add unit tests for SignalRRealtimeNotifier 8 event methods"
```
