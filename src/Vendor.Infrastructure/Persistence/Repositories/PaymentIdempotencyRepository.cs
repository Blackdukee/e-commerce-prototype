using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Infrastructure.Persistence.Repositories;

public class PaymentIdempotencyRepository(VendorDbContext context) : IPaymentIdempotencyRepository
{
    public async Task<PaymentIdempotencyKey?> GetByKeyUuidAsync(Guid keyUuid, CancellationToken ct = default)
    {
        return await context.PaymentIdempotencyKeys
            .FirstOrDefaultAsync(k => k.KeyUuid == keyUuid, ct);
    }

    public async Task AddAsync(PaymentIdempotencyKey key, CancellationToken ct = default)
    {
        await context.PaymentIdempotencyKeys.AddAsync(key, ct);
    }

    public Task UpdateAsync(PaymentIdempotencyKey key, CancellationToken ct = default)
    {
        context.PaymentIdempotencyKeys.Update(key);
        return Task.CompletedTask;
    }
}
