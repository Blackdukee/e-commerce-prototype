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
