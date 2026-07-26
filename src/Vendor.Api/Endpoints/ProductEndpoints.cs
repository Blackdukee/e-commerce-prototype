using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        var publicProducts = group.MapGroup("/products")
            .WithTags("Products")
            .RequireRateLimiting("catalog");

        publicProducts.MapGet("/", async (int? page, int? pageSize, string? category, string? tag, string? search, ISender mediator) =>
        {
            return Results.Ok(new ProductListResponse(
                new[] { new ProductSummaryDto(Guid.NewGuid(), "Sample Product", "sample-product", 49.99m, "USD", "Active", new[] { "https://img.svg" }) },
                1, page ?? 1, pageSize ?? 20
            ));
        });

        publicProducts.MapPost("/", async (CreateProductRequest req, HttpContext context, ISender mediator) =>
        {
            if (!context.User.IsInRole("VendorAdmin") && !context.User.IsInRole("Admin"))
            {
                return Results.Forbid();
            }
            return Results.Created($"/api/v1/products/{Guid.NewGuid()}", req);
        })
        .RequireAuthorization();

        publicProducts.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            return Results.Ok(new ProductDetailDto(
                id, "Sample Product", "sample-product", "Sample description", 49.99m, "USD", "Active",
                new[] { "tag1" }, new[] { "cat1" }, new[] { "https://img.svg" }, Array.Empty<ProductVariantDto>()
            ));
        });

        publicProducts.MapGet("/slug/{slug}", async (string slug, ISender mediator) =>
        {
            return Results.Ok(new ProductDetailDto(
                Guid.NewGuid(), "Sample Product", slug, "Sample description", 49.99m, "USD", "Active",
                new[] { "tag1" }, new[] { "cat1" }, new[] { "https://img.svg" }, Array.Empty<ProductVariantDto>()
            ));
        });

        var adminProducts = group.MapGroup("/admin/products")
            .WithTags("Admin Products")
            .RequireAuthorization();

        adminProducts.MapPost("/", async (CreateProductRequest req, ISender mediator) =>
        {
            return Results.Created($"/api/v1/products/{Guid.NewGuid()}", req);
        });

        adminProducts.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest req, ISender mediator) =>
        {
            return Results.Ok(req);
        });

        adminProducts.MapPut("/{id:guid}/stock", async (Guid id, AdjustStockRequest req, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminProducts.MapPost("/{id:guid}/activate", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminProducts.MapPost("/{id:guid}/deactivate", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminProducts.MapDelete("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminProducts.MapPost("/{id:guid}/variants", async (Guid id, CreateVariantRequest req, ISender mediator) =>
        {
            return Results.Created($"/api/v1/products/{id}", req);
        });

        adminProducts.MapPut("/{id:guid}/variants/{variantId:guid}", async (Guid id, Guid variantId, CreateVariantRequest req, ISender mediator) =>
        {
            return Results.Ok(req);
        });

        adminProducts.MapPost("/{id:guid}/images", async (Guid id, IFormFile image, ISender mediator) =>
        {
            return Results.Created($"/api/v1/products/{id}/images", new { url = "https://cdn.vendor.com/img.png" });
        });

        adminProducts.MapDelete("/{id:guid}/images", async (Guid id, string url, ISender mediator) =>
        {
            return Results.NoContent();
        });

        return group;
    }

    private record ProductListResponse(ProductSummaryDto[] Items, int TotalCount, int Page, int PageSize);
}
