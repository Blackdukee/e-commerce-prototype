using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(ProductId id, CancellationToken ct = default);
    Task<Product?> GetBySlugAsync(Slug slug, CancellationToken ct = default);
    Task<Product?> GetByVariantIdAsync(ProductVariantId variantId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> SearchAsync(string? searchTerm, int pageIndex, int pageSize, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task AddVariantAsync(Product product, ProductVariant variant, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
    Task<bool> ExistsAsync(ProductId id, CancellationToken ct = default);
}
