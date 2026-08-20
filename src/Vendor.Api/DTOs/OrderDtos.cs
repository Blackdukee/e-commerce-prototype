namespace Vendor.Api.DTOs;

public record CancelOrderRequest(string? Reason);
public record RefundRequestInputDto(string Reason, Guid[] LineIds);
public record AddOrderNoteRequest(string Note);

public record OrderDto(
    Guid Id,
    string OrderNumber,
    string Status,
    OrderLineDto[] Lines,
    AddressDto ShippingAddress,
    MoneyDto Subtotal,
    MoneyDto Tax,
    MoneyDto ShippingCost,
    MoneyDto Discount,
    MoneyDto Total,
    DateTime PlacedAtUtc
);

public record OrderLineDto(
    Guid VariantId,
    string ProductName,
    string Sku,
    int Quantity,
    MoneyDto UnitPrice,
    MoneyDto LineTotal
);

public record PaymentDto(
    Guid Id,
    Guid OrderId,
    string Provider,
    string Status,
    MoneyDto Amount,
    string? ExternalRef,
    DateTime CreatedAtUtc
);

public record CapturePaymentRequest(MoneyDto? Amount);
public record RefundPaymentRequest(MoneyDto Amount, string Reason);

public record ShippingRatesRequest(
    AddressDto Origin,
    AddressDto Destination,
    decimal WeightKg,
    decimal LengthCm,
    decimal WidthCm,
    decimal HeightCm
);

public record ShippingRateDto(
    string ServiceCode,
    string ServiceName,
    MoneyDto Cost,
    int EstimatedDaysMin,
    int EstimatedDaysMax
);

public record ShipmentDto(
    Guid Id,
    Guid OrderId,
    string? TrackingNumber,
    string? LabelUrl,
    string CarrierCode,
    string Status
);

public record CreateShipmentRequest(Guid OrderId, string ServiceCode, string CarrierCode);
public record TrackingResponseDto(string TrackingNumber, string Status, string? CurrentLocation, DateTime LastUpdatedUtc);

public record SubmitReturnRequest(Guid OrderId, ReturnItemInputDto[] Items, string Type, string Reason);
public record ReturnItemInputDto(Guid OrderLineId, int Quantity, Guid? ExchangeVariantId);
public record RejectReturnRequest(string Reason);
public record ApproveReturnRequestDto(string? Resolution);
public record CompleteExchangeRequest(Guid ReplacementVariantId, int ReplacementQuantity);

public record ReturnRequestDto(
    Guid Id,
    Guid OrderId,
    string Status,
    string Type,
    ReturnItemInputDto[] Items,
    DateTime SubmittedAtUtc
);
