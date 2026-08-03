using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.ReturnRequest;

namespace Vendor.Domain.Interfaces.Adapters;

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

    Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken ct = default);
}
