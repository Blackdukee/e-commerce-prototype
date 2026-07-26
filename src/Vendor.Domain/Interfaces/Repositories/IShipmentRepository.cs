using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Shipment;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(ShipmentId id, CancellationToken ct = default);
    Task<Shipment?> GetByOrderIdAsync(OrderId orderId, CancellationToken ct = default);
    Task AddAsync(Shipment shipment, CancellationToken ct = default);
    Task UpdateAsync(Shipment shipment, CancellationToken ct = default);
}
