namespace Vendor.Api.DTOs;

public record AddCartItemRequest(Guid VariantId, int Quantity);
public record UpdateCartItemRequest(int Quantity);
public record ApplyDiscountRequest(string Code);
public record MergeCartRequest(string GuestSessionId);

public record CartDto(
    Guid Id,
    CartItemDto[] Items,
    string? DiscountCode,
    MoneyDto Subtotal,
    MoneyDto Total
);

public record CartItemDto(
    Guid VariantId,
    string ProductName,
    string Sku,
    int Quantity,
    MoneyDto UnitPrice,
    MoneyDto LineTotal
);

public record CheckoutRequest(
    AddressDto ShippingAddress,
    string ShippingServiceCode,
    string PaymentProvider,
    Guid? CartId = null
);

public record CheckoutResponseDto(
    Guid OrderId,
    string OrderNumber,
    MoneyDto Total,
    PaymentInitDto PaymentInit
);

public record PaymentInitDto(
    string Provider,
    string? ClientSecret,
    string? ApprovalUrl,
    string? PaymentKey
);

public record MoneyDto(decimal Amount, string Currency);
