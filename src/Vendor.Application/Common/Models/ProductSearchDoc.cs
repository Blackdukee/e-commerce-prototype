namespace Vendor.Application.Common.Models;

public record ProductSearchDoc(
    string Id,
    string Name,
    string Slug,
    string? Description,
    decimal BasePrice,
    string Currency,
    string Status,
    DateTime CreatedAtUtc);
