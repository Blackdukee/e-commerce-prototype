using Elastic.Clients.Elasticsearch;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;
using Vendor.Application.Modules.Customers.Queries;

namespace Vendor.Infrastructure.Search;

public class ElasticsearchProductSearchService : IProductSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ElasticsearchProductSearchService(ElasticsearchClient client, string indexName)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _indexName = !string.IsNullOrWhiteSpace(indexName)
            ? indexName
            : throw new ArgumentNullException(nameof(indexName));
    }

    public async Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query,
        ProductSearchFilters filters,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var from = (page - 1) * pageSize;

        var response = await _client.SearchAsync<ProductSearchDoc>(s =>
        {
            s.Index(_indexName).From(from).Size(pageSize);

            s.Query(q =>
            {
                q.Bool(b =>
                {
                    var musts = new List<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<ProductSearchDoc>>>();

                    musts.Add(m => m.Term(t => t.Field("status").Value(filters.Status ?? "Active")));

                    if (!string.IsNullOrWhiteSpace(filters.Category))
                    {
                        musts.Add(m => m.Term(t => t.Field("category").Value(filters.Category)));
                    }

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var queryText = query;
                        musts.Add(m => m.MultiMatch(mm => mm
                            .Fields(Elastic.Clients.Elasticsearch.Infer.Fields<ProductSearchDoc>(
                                f => f.Name, f => f.Description!))
                            .Query(queryText)));
                    }

                    if (filters.MinPrice.HasValue || filters.MaxPrice.HasValue)
                    {
                        musts.Add(m => m.Range(r => r.NumberRange(nr =>
                        {
                            nr.Field("basePrice");
                            if (filters.MinPrice.HasValue) nr.Gte((double)filters.MinPrice.Value);
                            if (filters.MaxPrice.HasValue) nr.Lte((double)filters.MaxPrice.Value);
                        })));
                    }

                    b.Must(musts.ToArray());
                });
            });
        }, ct);

        if (!response.IsValidResponse)
            return new PagedResult<ProductSearchDoc>([], 0, page, pageSize);

        var total = (int)response.Total;
        return new PagedResult<ProductSearchDoc>(response.Documents.ToList(), total, page, pageSize);
    }

    public async Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default)
        => await _client.IndexAsync(doc, i => i.Index(_indexName).Id(doc.Id), ct);

    public async Task DeleteProductIndexAsync(string productId, CancellationToken ct = default)
        => await _client.DeleteAsync(_indexName, productId, ct);
}
