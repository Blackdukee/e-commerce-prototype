using Microsoft.EntityFrameworkCore;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Domain.Aggregates.Product;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Search;

public class ProductIndexSyncJob(VendorDbContext dbContext, IProductSearchService searchService)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active)
            .ToListAsync(ct);

        foreach (var product in products)
        {
            var doc = new ProductSearchDoc(
                product.Id.Value.ToString(),
                product.Name,
                product.Slug.Value,
                product.Description,
                product.BasePrice.Amount,
                product.BasePrice.Currency,
                product.Status.ToString(),
                product.CreatedAtUtc);

            await searchService.IndexProductAsync(doc, ct);
        }
    }
}
