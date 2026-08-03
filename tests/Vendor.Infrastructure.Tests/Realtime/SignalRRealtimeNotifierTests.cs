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
        var address = new AddressDto("123 Main St", "City", "State", "12345", "US");
        var dto = new OrderDto(
            Guid.NewGuid(),
            "ORD-001",
            Guid.NewGuid(),
            "Pending",
            address,
            80.00m,
            9.99m,
            10.00m,
            0m,
            99.99m,
            DateTime.UtcNow,
            Array.Empty<OrderLineDto>());

        await _notifier.NotifyNewOrderAsync(dto);
        _mockAdminClient.Verify(c => c.OnNewOrder(dto), Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentReceivedAsync_DispatchesOnPaymentReceived()
    {
        var dto = new PaymentDto(Guid.NewGuid(), Guid.NewGuid(), "Completed", 99.99m, "USD", "IDEM-001", "Stripe", DateTime.UtcNow);
        await _notifier.NotifyPaymentReceivedAsync(dto);
        _mockAdminClient.Verify(c => c.OnPaymentReceived(dto), Times.Once);
    }

    [Fact]
    public async Task NotifyPaymentFailedAsync_DispatchesOnPaymentFailed()
    {
        var dto = new PaymentDto(Guid.NewGuid(), Guid.NewGuid(), "Failed", 99.99m, "USD", "IDEM-002", "Stripe", DateTime.UtcNow);
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
        var dto = new ReturnRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Pending", "Defective", "Refund", DateTime.UtcNow, Array.Empty<ReturnItemDto>());
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
        var config = new VendorConfigDto("acme", "ACME Store", 1, DateTime.UtcNow);
        await _notifier.NotifySettingsUpdatedAsync(config);
        _mockAdminClient.Verify(c => c.OnSettingsUpdated(config), Times.Once);
    }
}
