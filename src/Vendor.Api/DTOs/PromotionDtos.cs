namespace Vendor.Api.DTOs;

public record ValidatePromotionRequest(string Code);
public record ValidatePromotionResponse(bool Valid, MoneyDto? DiscountAmount, string? ErrorMessage);

public record CreatePromotionRequest(
    string Code,
    string DiscountType,
    decimal Value,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    int? MaxUsages
);

public record PromotionDto(
    Guid Id,
    string Code,
    string DiscountType,
    decimal Value,
    bool IsActive,
    int UsageCount
);

public record AnalyticsSummaryDto(
    int TotalOrders,
    decimal TotalRevenue,
    int ActiveCustomers,
    int AbandonedCartsCount
);

public record UpdateBrandingRequest(string PrimaryColor, string SecondaryColor, string LogoUrl, string FontFamily);
public record UpdateCheckoutRequest(bool GuestCheckoutEnabled, int MaxItemsPerOrder, string OrderNumberPrefix);
public record UpdateShippingRequest(string DefaultProvider, decimal BaseRate);
public record UpdateFeatureFlagsRequest(Dictionary<string, bool> Flags);
public record ToggleMaintenanceRequest(bool Enabled);
