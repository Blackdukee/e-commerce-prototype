using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Models;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
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
    IReadOnlyList<string> Images,
    string? Category = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<string>? Tags = null,
    string? Description = null)
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
        product.Images.ToList(),
        product.Category,
        product.Categories.ToList(),
        product.Tags.ToList(),
        product.Description);
}

public record CreateProductCommand(
    string Name,
    string Slug,
    decimal BasePrice,
    string Currency,
    int LowStockThreshold = 3,
    string? Description = null,
    string? Category = null,
    List<string>? Categories = null,
    List<string>? Tags = null,
    List<string>? Images = null) : ICommand<Result<ProductDto>>;

public record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string Slug,
    decimal BasePrice,
    string? Description = null,
    string? Category = null,
    List<string>? Categories = null,
    List<string>? Tags = null) : ICommand<Result<ProductDto>>;
public record ActivateProductCommand(Guid ProductId) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => ToGuidString($"ACTIVATE-{ProductId}");
    private static string ToGuidString(string input) => new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToString();
}
public record DeactivateProductCommand(Guid ProductId, string? Reason = null) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => ToGuidString($"DEACTIVATE-{ProductId}");
    private static string ToGuidString(string input) => new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToString();
}
public record AddProductVariantCommand(Guid ProductId, string Sku, decimal PriceAdjustment, int StockQuantity, decimal WeightKg) : ICommand<Result<ProductVariantDto>>;
public record UpdateProductVariantCommand(Guid VariantId, decimal PriceAdjustment, int StockQuantity, decimal WeightKg) : ICommand<Result<ProductVariantDto>>;
public record DeleteProductVariantCommand(Guid ProductId, Guid VariantId) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => ToGuidString($"DEL-VAR-{VariantId}");
    private static string ToGuidString(string input) => new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToString();
}
public record AddProductImageCommand(Guid ProductId, string ImageUrl) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => ToGuidString($"ADD-IMG-{ProductId}-{ImageUrl}");
    private static string ToGuidString(string input) => new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToString();
}
public record RemoveProductImageCommand(Guid ProductId, string ImageUrl) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => ToGuidString($"REM-IMG-{ProductId}-{ImageUrl}");
    private static string ToGuidString(string input) => new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input))).ToString();
}

public record GetProductByIdQuery(Guid ProductId) : IQuery<Result<ProductDto>>;
public record GetProductBySlugQuery(string Slug) : IQuery<Result<ProductDto>>;
public record SearchProductsQuery(
    string? SearchTerm = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int PageIndex = 0,
    int PageSize = 20) : IQuery<Result<PagedResult<ProductDto>>>;
public record GetProductVariantsQuery(Guid ProductId) : IQuery<Result<IReadOnlyList<ProductVariantDto>>>;

public class CreateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var slug = new Slug(request.Slug);
        if (await productRepository.GetBySlugAsync(slug, ct) != null)
        {
            return Error.Conflict("Slug.Exists", $"Slug '{request.Slug}' is already in use.");
        }

        var product = new Product(
            ProductId.New(),
            request.Name,
            slug,
            new Money(request.BasePrice, request.Currency),
            request.Description,
            request.LowStockThreshold,
            request.Category,
            request.Categories,
            request.Tags);

        if (request.Images != null)
        {
            foreach (var img in request.Images)
            {
                if (!string.IsNullOrWhiteSpace(img))
                {
                    product.AddImage(img);
                }
            }
        }

        await productRepository.AddAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);

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

public class GetProductBySlugQueryHandler(IProductRepository productRepository) : IRequestHandler<GetProductBySlugQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductBySlugQuery request, CancellationToken ct)
    {
        var product = await productRepository.GetBySlugAsync(new Slug(request.Slug), ct);
        if (product == null) return Error.NotFound("Product", request.Slug);
        return ProductDto.FromDomain(product);
    }
}

public class SearchProductsQueryHandler(IProductRepository productRepository) : IRequestHandler<SearchProductsQuery, Result<PagedResult<ProductDto>>>
{
    public async Task<Result<PagedResult<ProductDto>>> Handle(SearchProductsQuery request, CancellationToken ct)
    {
        var (products, totalCount) = await productRepository.SearchAsync(
            request.SearchTerm,
            request.Category,
            request.MinPrice,
            request.MaxPrice,
            request.PageIndex,
            request.PageSize,
            ct);
        var dtos = products.Select(ProductDto.FromDomain).ToList();
        return new PagedResult<ProductDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}

public class UpdateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(new ProductId(request.ProductId), ct);
        if (product == null) return Error.NotFound("Product", request.ProductId);

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? product.Slug : new Slug(request.Slug);
        var price = request.BasePrice > 0 ? new Money(request.BasePrice, product.BasePrice.Currency) : product.BasePrice;
        product.UpdateDetails(request.Name, slug, price, request.Description, request.Category, request.Categories, request.Tags);

        await productRepository.UpdateAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ProductDto.FromDomain(product);
    }
}

public class ActivateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<ActivateProductCommand, Result>
{
    public async Task<Result> Handle(ActivateProductCommand request, CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(new ProductId(request.ProductId), ct);
        if (product == null) return Error.NotFound("Product", request.ProductId);
        product.Activate();
        await productRepository.UpdateAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class DeactivateProductCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<DeactivateProductCommand, Result>
{
    public async Task<Result> Handle(DeactivateProductCommand request, CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(new ProductId(request.ProductId), ct);
        if (product == null) return Error.NotFound("Product", request.ProductId);
        product.Discontinue();
        await productRepository.UpdateAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class AddProductVariantCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<AddProductVariantCommand, Result<ProductVariantDto>>
{
    public async Task<Result<ProductVariantDto>> Handle(AddProductVariantCommand request, CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(new ProductId(request.ProductId), ct);
        if (product == null) return Error.NotFound("Product", request.ProductId);

        var variant = new ProductVariant(
            ProductVariantId.New(),
            product.Id,
            request.Sku,
            new Money(request.PriceAdjustment, product.BasePrice.Currency),
            request.StockQuantity,
            new Weight(request.WeightKg, WeightUnit.Kg),
            new Dimensions(10, 10, 10, DimensionUnit.Cm));

        product.AddVariant(variant);
        await productRepository.AddVariantAsync(product, variant, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductVariantDto(variant.Id.Value, variant.Sku, variant.PriceAdjustment.Amount, variant.StockQuantity, variant.Weight.Value, variant.Weight.Unit.ToString());
    }
}

public class UpdateProductVariantCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<UpdateProductVariantCommand, Result<ProductVariantDto>>
{
    public async Task<Result<ProductVariantDto>> Handle(UpdateProductVariantCommand request, CancellationToken ct)
    {
        var product = await productRepository.GetByVariantIdAsync(new ProductVariantId(request.VariantId), ct);
        if (product == null) return Error.NotFound("ProductVariant", request.VariantId);

        var variant = product.Variants.FirstOrDefault(v => v.Id.Value == request.VariantId);
        if (variant == null) return Error.NotFound("ProductVariant", request.VariantId);

        variant.UpdateDetails(new Money(request.PriceAdjustment, product.BasePrice.Currency), request.StockQuantity, new Weight(request.WeightKg, WeightUnit.Kg));
        await productRepository.UpdateAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProductVariantDto(variant.Id.Value, variant.Sku, variant.PriceAdjustment.Amount, variant.StockQuantity, variant.Weight.Value, variant.Weight.Unit.ToString());
    }
}

public class AddProductImageCommandHandler(IProductRepository productRepository, IUnitOfWork unitOfWork) : IRequestHandler<AddProductImageCommand, Result>
{
    public async Task<Result> Handle(AddProductImageCommand request, CancellationToken ct)
    {
        var product = await productRepository.GetByIdAsync(new ProductId(request.ProductId), ct);
        if (product == null) return Error.NotFound("Product", request.ProductId);

        product.AddImage(request.ImageUrl);
        await productRepository.UpdateAsync(product, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
