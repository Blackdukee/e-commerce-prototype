namespace Vendor.Application.Common.Models;

public record ProductSearchFilters(
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Status = "Active");
