using Vendor.Domain.Aggregates.Promotion;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(PromotionId id, CancellationToken ct = default);
    Task<Promotion?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(Promotion promotion, CancellationToken ct = default);
    Task UpdateAsync(Promotion promotion, CancellationToken ct = default);
}
