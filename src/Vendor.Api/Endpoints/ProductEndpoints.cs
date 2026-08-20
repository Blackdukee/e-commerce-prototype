using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Api.Extensions;
using Vendor.Application.Modules.Products;

namespace Vendor.Api.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        var publicProducts = group.MapGroup("/products")
            .WithTags("Products");

        publicProducts.MapGet("/", async (int? page, int? pageSize, string? search, string? category, decimal? minPrice, decimal? maxPrice, ISender mediator, CancellationToken ct) =>
        {
            var pIndex = (page ?? 1) - 1;
            var pSize = Math.Min(pageSize ?? 20, 100);
            var result = await mediator.Send(new SearchProductsQuery(search, category, minPrice, maxPrice, pIndex <= 0 ? 0 : pIndex, pSize <= 0 ? 20 : pSize), ct);
            return result.ToHttpResult();
        });

        publicProducts.MapPost("/", async (CreateProductRequest req, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            if (!context.User.IsInRole("VendorAdmin") && !context.User.IsInRole("Admin") && !context.User.IsInRole("SuperAdmin"))
            {
                return Results.Forbid();
            }
            var command = new CreateProductCommand(
                req.Name,
                req.Slug,
                req.BasePriceAmount,
                req.Currency,
                3,
                req.Description,
                req.Categories?.FirstOrDefault(),
                req.Categories?.ToList(),
                req.Tags?.ToList(),
                req.Images?.ToList());
            var result = await mediator.Send(command, ct);
            return result.ToCreatedHttpResult($"/api/v1/products/{result.Value?.Id}");
        })
        .RequireAuthorization();

        publicProducts.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProductByIdQuery(id), ct);
            return result.ToHttpResult();
        });

        publicProducts.MapGet("/slug/{slug}", async (string slug, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProductBySlugQuery(slug), ct);
            return result.ToHttpResult();
        });

        var adminProducts = group.MapGroup("/admin/products")
            .WithTags("Admin Products")
            .RequireAuthorization();

        adminProducts.MapPost("/", async (CreateProductRequest req, ISender mediator, CancellationToken ct) =>
        {
            var command = new CreateProductCommand(
                req.Name,
                req.Slug,
                req.BasePriceAmount,
                req.Currency,
                3,
                req.Description,
                req.Categories?.FirstOrDefault(),
                req.Categories?.ToList(),
                req.Tags?.ToList(),
                req.Images?.ToList());
            var result = await mediator.Send(command, ct);
            return result.ToCreatedHttpResult($"/api/v1/products/{result.Value?.Id}");
        });

        adminProducts.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest req, ISender mediator, CancellationToken ct) =>
        {
            var command = new UpdateProductCommand(
                id,
                req.Name ?? "",
                req.Slug ?? "",
                req.BasePriceAmount ?? 0m,
                req.Description,
                req.Categories?.FirstOrDefault(),
                req.Categories?.ToList(),
                req.Tags?.ToList());
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        adminProducts.MapPost("/{id:guid}/activate", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateProductCommand(id), ct);
            return result.ToHttpResult();
        });

        adminProducts.MapPost("/{id:guid}/deactivate", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateProductCommand(id), ct);
            return result.ToHttpResult();
        });

        adminProducts.MapPost("/{id:guid}/images", async (Guid id, AddProductImageRequest req, ISender mediator, CancellationToken ct) =>
        {
            var command = new AddProductImageCommand(id, req.ImageUrl);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        adminProducts.MapPost("/{id:guid}/variants", async (Guid id, CreateVariantRequest req, ISender mediator, CancellationToken ct) =>
        {
            var command = new AddProductVariantCommand(id, req.Sku, req.PriceAdjustmentAmount, req.InitialStock, req.WeightValue);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        adminProducts.MapPut("/{id:guid}/variants/{variantId:guid}", async (Guid id, Guid variantId, CreateVariantRequest req, ISender mediator, CancellationToken ct) =>
        {
            var command = new UpdateProductVariantCommand(variantId, req.PriceAdjustmentAmount, req.InitialStock, req.WeightValue);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        return group;
    }
}
