using Microsoft.EntityFrameworkCore;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;
using Vendor.Domain.Aggregates.Product;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Search;

public class EfCoreProductSearchService(VendorDbContext dbContext) : IProductSearchService
{
    public async Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query,
        ProductSearchFilters filters,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = dbContext.Products.AsNoTracking();

        var statusFilter = filters.Status ?? "Active";
        if (Enum.TryParse<ProductStatus>(statusFilter, ignoreCase: true, out var status))
            q = q.Where(p => p.Status == status);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query}%";
            q = q.Where(p =>
                EF.Functions.Like(p.Name, pattern) ||
                EF.Functions.Like(p.Description ?? "", pattern));
        }

        if (filters.MinPrice.HasValue)
            q = q.Where(p => p.BasePrice.Amount >= filters.MinPrice.Value);

        if (filters.MaxPrice.HasValue)
            q = q.Where(p => p.BasePrice.Amount <= filters.MaxPrice.Value);

        var totalCount = await q.CountAsync(ct);
        var skip = (page - 1) * pageSize;

        var items = await q
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(p => new ProductSearchDoc(
                p.Id.Value.ToString(),
                p.Name,
                p.Slug.Value,
                p.Description,
                p.BasePrice.Amount,
                p.BasePrice.Currency,
                p.Status.ToString(),
                p.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<ProductSearchDoc>(items, totalCount, page, pageSize);
    }

    public Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteProductIndexAsync(string productId, CancellationToken ct = default) => Task.CompletedTask;
}
