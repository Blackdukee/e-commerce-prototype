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

public class AdminNotificationHub : Hub<IAdminNotificationClient>;

public interface IRealtimeNotifier
{
    Task NotifyNewOrderAsync(OrderDto order, CancellationToken ct = default);
    Task NotifyPaymentReceivedAsync(PaymentDto payment, CancellationToken ct = default);
}

public class SignalRRealtimeNotifier(IHubContext<AdminNotificationHub, IAdminNotificationClient> hubContext) : IRealtimeNotifier
{
    public Task NotifyNewOrderAsync(OrderDto order, CancellationToken ct = default)
    {
        return hubContext.Clients.All.OnNewOrder(order);
    }

    public Task NotifyPaymentReceivedAsync(PaymentDto payment, CancellationToken ct = default)
    {
        return hubContext.Clients.All.OnPaymentReceived(payment);
    }
}
