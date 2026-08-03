using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Models;

namespace Vendor.Api.Endpoints;

public static class ProductSearchEndpoints
{
    public static RouteGroupBuilder MapProductSearchEndpoints(this RouteGroupBuilder group)
    {
        var products = group.MapGroup("/products").WithTags("Products");

        products.MapGet("/search", async (
            string? q,
            decimal? minPrice,
            decimal? maxPrice,
            string? status,
            int page,
            int pageSize,
            IProductSearchService searchService,
            CancellationToken ct) =>
        {
            page = page < 1 ? 1 : page;
            if (pageSize < 1 || pageSize > 100)
                return Results.BadRequest(new { Error = "pageSize must be between 1 and 100." });

            var filters = new ProductSearchFilters(minPrice, maxPrice, status ?? "Active");
            var result = await searchService.SearchProductsAsync(q, filters, page, pageSize, ct);
            return Results.Ok(result);
        })
        .WithName("SearchProducts");

        return group;
    }
}
