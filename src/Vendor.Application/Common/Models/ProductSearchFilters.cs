namespace Vendor.Application.Common.Models;

public record ProductSearchFilters(
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Status = "Active");
