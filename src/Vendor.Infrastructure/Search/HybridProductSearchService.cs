using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;

namespace Vendor.Infrastructure.Search;

public class HybridProductSearchService : IProductSearchService
{
    private readonly IProductSearchService _efCoreService;
    private readonly IProductSearchService? _elasticsearchService;

    public HybridProductSearchService(
        EfCoreProductSearchService efCoreService,
        IProductSearchService? elasticsearchService = null)
    {
        _efCoreService = efCoreService ?? throw new ArgumentNullException(nameof(efCoreService));
        _elasticsearchService = elasticsearchService;
    }

    private IProductSearchService Active => _elasticsearchService ?? _efCoreService;

    public Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query, ProductSearchFilters filters, int page, int pageSize, CancellationToken ct = default)
        => Active.SearchProductsAsync(query, filters, page, pageSize, ct);

    public Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default)
        => Active.IndexProductAsync(doc, ct);

    public Task DeleteProductIndexAsync(string productId, CancellationToken ct = default)
        => Active.DeleteProductIndexAsync(productId, ct);
}
