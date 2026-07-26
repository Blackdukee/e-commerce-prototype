namespace Vendor.Api.DTOs;

public record CreateProductRequest(
    string Name,
    string Slug,
    string Description,
    decimal BasePriceAmount,
    string Currency,
    string[] Tags,
    string[] Categories,
    string[] Images
);

public record UpdateProductRequest(
    string? Name,
    string? Description,
    decimal? BasePriceAmount,
    string? Currency,
    string[]? Tags,
    string[]? Categories
);

public record AdjustStockRequest(Guid VariantId, int Delta, string Reason);

public record CreateVariantRequest(
    string Sku,
    decimal PriceAdjustmentAmount,
    string Currency,
    int InitialStock,
    decimal WeightValue,
    string WeightUnit,
    decimal Length,
    decimal Width,
    decimal Height,
    string DimensionUnit
);

public record ProductSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    decimal BasePriceAmount,
    string Currency,
    string Status,
    string[] Images
);

public record ProductDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    decimal BasePriceAmount,
    string Currency,
    string Status,
    string[] Tags,
    string[] Categories,
    string[] Images,
    ProductVariantDto[] Variants
);

public record ProductVariantDto(
    Guid Id,
    string Sku,
    int StockQuantity,
    decimal PriceAdjustmentAmount,
    string Currency
);
