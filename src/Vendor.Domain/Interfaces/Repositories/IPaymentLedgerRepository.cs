using Vendor.Domain.Aggregates.Payment;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IPaymentLedgerRepository
{
    Task<IReadOnlyList<PaymentLedgerEntry>> GetByPaymentIdAsync(PaymentId paymentId, CancellationToken ct = default);
    Task<int> GetNextSequenceNumberAsync(PaymentId paymentId, CancellationToken ct = default);
    Task AddAsync(PaymentLedgerEntry entry, CancellationToken ct = default);
}
