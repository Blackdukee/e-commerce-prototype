using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken ct = default);
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<Payment?> GetByOrderIdAsync(OrderId orderId, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
}
