using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Products;

public record ProductVariantDto(Guid Id, string Sku, decimal PriceAdjustment, int StockQuantity, decimal Weight, string WeightUnit);

public record ProductDto(
    Guid Id,
    string Name,
    string Slug,
    decimal BasePrice,
    string Currency,
    string Status,
    int LowStockThreshold,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<string> Images)
{
    public static ProductDto FromDomain(Product product) => new(
        product.Id.Value,
        product.Name,
        product.Slug.Value,
        product.BasePrice.Amount,
        product.BasePrice.Currency,
        product.Status.ToString(),
        product.LowStockThreshold,
        product.Variants.Select(v => new ProductVariantDto(v.Id.Value, v.Sku, v.PriceAdjustment.Amount, v.StockQuantity, v.Weight.Value, v.Weight.Unit.ToString())).ToList(),
        product.Images.ToList());
}

public record CreateProductCommand(string Name, string Slug, decimal BasePrice, string Currency, int LowStockThreshold = 3, string? Description = null) : ICommand<Result<ProductDto>>;
public record UpdateProductCommand(Guid ProductId, string Name, string Slug, decimal BasePrice, string? Description = null) : ICommand<Result<ProductDto>>;
public record ActivateProductCommand(Guid ProductId) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"ACTIVATE-{ProductId}";
}
public record DeactivateProductCommand(Guid ProductId, string? Reason = null) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"DEACTIVATE-{ProductId}";
}
public record AddProductVariantCommand(Guid ProductId, string Sku, decimal PriceAdjustment, int StockQuantity, decimal WeightKg) : ICommand<Result<ProductVariantDto>>;
public record UpdateProductVariantCommand(Guid VariantId, decimal PriceAdjustment, int StockQuantity, decimal WeightKg) : ICommand<Result<ProductVariantDto>>;
public record DeleteProductVariantCommand(Guid ProductId, Guid VariantId) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"DEL-VAR-{VariantId}";
}
public record AddProductImageCommand(Guid ProductId, string ImageUrl) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"ADD-IMG-{ProductId}-{ImageUrl.GetHashCode()}";
}
public record RemoveProductImageCommand(Guid ProductId, string ImageUrl) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"REM-IMG-{ProductId}-{ImageUrl.GetHashCode()}";
}

public record GetProductByIdQuery(Guid ProductId) : IQuery<Result<ProductDto>>;
public record GetProductBySlugQuery(string Slug) : IQuery<Result<ProductDto>>;
public record SearchProductsQuery(string? SearchTerm, int PageIndex = 0, int PageSize = 20) : IQuery<Result<IReadOnlyList<ProductDto>>>;
public record GetProductVariantsQuery(Guid ProductId) : IQuery<Result<IReadOnlyList<ProductVariantDto>>>;

public class CreateProductCommandHandler(IProductRepository productRepository) : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var slug = new Slug(request.Slug);
        if (await productRepository.GetBySlugAsync(slug, ct) != null)
        {
            return Error.Conflict("Slug.Exists", $"Slug '{request.Slug}' is already in use.");
        }

        var product = new Product(ProductId.New(), request.Name, slug, new Money(request.BasePrice, request.Currency), request.Description, request.LowStockThreshold);
        await productRepository.AddAsync(product, ct);

        return ProductDto.FromDomain(product);
    }
}

public class GetProductByIdQueryHandler(IProductRepository productRepository) : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(new ProductId(request.ProductId), ct);
        if (product == null) return Error.NotFound("Product", request.ProductId);
        return ProductDto.FromDomain(product);
    }
}
