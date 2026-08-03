using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;

namespace Vendor.Application.Common.Interfaces;

public interface IProductSearchService
{
    Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query,
        ProductSearchFilters filters,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default);
    Task DeleteProductIndexAsync(string productId, CancellationToken ct = default);
}
