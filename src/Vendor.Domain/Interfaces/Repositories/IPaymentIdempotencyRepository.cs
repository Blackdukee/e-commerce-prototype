using Vendor.Domain.Aggregates.Payment;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IPaymentIdempotencyRepository
{
    Task<PaymentIdempotencyKey?> GetByKeyUuidAsync(Guid keyUuid, CancellationToken ct = default);
    Task AddAsync(PaymentIdempotencyKey key, CancellationToken ct = default);
    Task UpdateAsync(PaymentIdempotencyKey key, CancellationToken ct = default);
}
