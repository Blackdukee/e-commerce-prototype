using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Infrastructure.Persistence.Repositories;

public class PaymentLedgerRepository(VendorDbContext context) : IPaymentLedgerRepository
{
    public async Task<IReadOnlyList<PaymentLedgerEntry>> GetByPaymentIdAsync(PaymentId paymentId, CancellationToken ct = default)
    {
        return await context.PaymentLedgerEntries
            .Where(e => e.PaymentId == paymentId)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync(ct);
    }

    public async Task<int> GetNextSequenceNumberAsync(PaymentId paymentId, CancellationToken ct = default)
    {
        var maxSeq = await context.PaymentLedgerEntries
            .Where(e => e.PaymentId == paymentId)
            .MaxAsync(e => (int?)e.SequenceNumber, ct);

        return (maxSeq ?? 0) + 1;
    }

    public async Task AddAsync(PaymentLedgerEntry entry, CancellationToken ct = default)
    {
        await context.PaymentLedgerEntries.AddAsync(entry, ct);
    }
}
