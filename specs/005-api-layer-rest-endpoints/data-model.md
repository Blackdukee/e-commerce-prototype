# Data Model: API Layer Composition Root & REST Endpoints

**Feature**: 005-api-layer-rest-endpoints  
**Date**: 2026-07-25

> The API layer is the composition root. It has **no new database entities**. All persistence entities are owned by the Domain/Infrastructure layers (Features 002-004). This document covers the API-specific request/response models (DTOs, request contracts, ProblemDetails schemas) used at the HTTP boundary.

---

## API Request / Response DTOs

### Auth Module

| DTO | Direction | Fields |
|-----|-----------|--------|
| `RegisterRequest` | Request | `string Email`, `string FirstName`, `string LastName`, `string Password` |
| `LoginRequest` | Request | `string Email`, `string Password` |
| `GuestSessionRequest` | Request | `string? SessionId` |
| `RefreshTokenRequest` | Request | `string RefreshToken` |
| `RevokeTokenRequest` | Request | `string RefreshToken` |
| `ExternalAuthRequest` | Request | `string Provider`, `string IdToken` |
| `ForgotPasswordRequest` | Request | `string Email` |
| `ResetPasswordRequest` | Request | `string Email`, `string Token`, `string NewPassword` |
| `AuthResponse` | Response | `string AccessToken`, `string RefreshToken`, `DateTime ExpiresAt`, `CustomerDto Customer` |
| `CustomerDto` | Response | `Guid Id`, `string Email`, `string FirstName`, `string LastName`, `string CustomerType` |

### Product Module

| DTO | Direction | Fields |
|-----|-----------|--------|
| `ProductListRequest` | Query Params | `int Page=1`, `int PageSize=20`, `string? Category`, `string? Tag`, `string? Search`, `string? Sort` |
| `ProductListResponse` | Response | `IReadOnlyList<ProductSummaryDto> Items`, `int TotalCount`, `int Page`, `int PageSize` |
| `ProductSummaryDto` | Response | `Guid Id`, `string Name`, `string Slug`, `decimal BasePrice`, `string Currency`, `string Status`, `string[] Images` |
| `ProductDetailDto` | Response | All `ProductSummaryDto` fields + `string Description`, `string[] Tags`, `string[] Categories`, `ProductVariantDto[] Variants` |
| `ProductVariantDto` | Response | `Guid Id`, `string Sku`, `int StockQuantity`, `decimal PriceAdjustment`, `string Currency` |
| `CreateProductRequest` | Request | `string Name`, `string Slug`, `string Description`, `decimal BasePrice`, `string Currency`, `string[] Tags`, `string[] Categories`, `string[] Images` |
| `UpdateProductRequest` | Request | `string? Name`, `string? Description`, `decimal? BasePrice`, `string? Currency`, `string[]? Tags`, `string[]? Categories` |
| `AdjustStockRequest` | Request | `Guid VariantId`, `int Delta`, `string Reason` |
| `CreateVariantRequest` | Request | `string Sku`, `decimal PriceAdjustment`, `string Currency`, `int InitialStock`, `decimal Weight`, `string WeightUnit`, `decimal Length`, `decimal Width`, `decimal Height`, `string DimensionUnit` |

### Cart Module

| DTO | Direction | Fields |
|-----|-----------|--------|
| `CartDto` | Response | `Guid Id`, `CartItemDto[] Items`, `string? DiscountCode`, `MoneyDto Subtotal`, `MoneyDto Total` |
| `CartItemDto` | Response | `Guid VariantId`, `string ProductName`, `string Sku`, `int Quantity`, `MoneyDto UnitPrice`, `MoneyDto LineTotal` |
| `AddCartItemRequest` | Request | `Guid VariantId`, `int Quantity` |
| `UpdateCartItemRequest` | Request | `int Quantity` |
| `ApplyDiscountRequest` | Request | `string Code` |
| `MergeCartRequest` | Request | `string GuestSessionId` |
| `CheckoutRequest` | Request | `AddressDto ShippingAddress`, `string ShippingServiceCode`, `string PaymentProvider` |
| `CheckoutResponse` | Response | `Guid OrderId`, `string OrderNumber`, `MoneyDto Total`, `PaymentInitDto PaymentInit` |
| `PaymentInitDto` | Response | `string Provider`, `string? ClientSecret`, `string? ApprovalUrl`, `string? PaymentKey` |

### Order Module

| DTO | Direction | Fields |
|-----|-----------|--------|
| `OrderDto` | Response | `Guid Id`, `string OrderNumber`, `string Status`, `OrderLineDto[] Lines`, `AddressDto ShippingAddress`, `MoneyDto Subtotal`, `MoneyDto Tax`, `MoneyDto ShippingCost`, `MoneyDto Discount`, `MoneyDto Total`, `DateTime PlacedAtUtc` |
| `OrderLineDto` | Response | `Guid VariantId`, `string ProductName`, `string Sku`, `int Quantity`, `MoneyDto UnitPrice`, `MoneyDto LineTotal` |
| `OrderListResponse` | Response | `IReadOnlyList<OrderSummaryDto> Items`, `int TotalCount`, `int Page`, `int PageSize` |
| `OrderSummaryDto` | Response | `Guid Id`, `string OrderNumber`, `string Status`, `MoneyDto Total`, `DateTime PlacedAtUtc` |
| `CancelOrderRequest` | Request | `string? Reason` |
| `RefundRequestDto` | Request | `string Reason`, `Guid[] LineIds` |
| `AddOrderNoteRequest` | Request | `string Note` |

### Payment Module

| DTO | Direction | Fields |
|-----|-----------|--------|
| `PaymentDto` | Response | `Guid Id`, `Guid OrderId`, `string Provider`, `string Status`, `MoneyDto Amount`, `string? ExternalRef`, `DateTime CreatedAtUtc` |
| `CapturePaymentRequest` | Request | `MoneyDto? Amount` (null = full capture) |
| `RefundPaymentRequest` | Request | `MoneyDto Amount`, `string Reason` |

### Shipment Module

| DTO | Direction | Fields |
|-----|-----------|--------|
| `ShippingRatesRequest` | Request | `AddressDto Origin`, `AddressDto Destination`, `decimal WeightKg`, `decimal LengthCm`, `decimal WidthCm`, `decimal HeightCm` |
| `ShippingRatesResponse` | Response | `ShippingRateDto[] Rates` |
| `ShippingRateDto` | Response | `string ServiceCode`, `string ServiceName`, `MoneyDto Cost`, `int EstimatedDaysMin`, `int EstimatedDaysMax` |
| `ShipmentDto` | Response | `Guid Id`, `Guid OrderId`, `string? TrackingNumber`, `string? LabelUrl`, `string CarrierCode`, `string Status` |
| `CreateShipmentRequest` | Request | `Guid OrderId`, `string ServiceCode`, `string CarrierCode` |
| `TrackingResponse` | Response | `string TrackingNumber`, `string Status`, `string? CurrentLocation`, `DateTime LastUpdatedUtc` |

### Return Module

| DTO | Direction | Fields |
|-----|-----------|--------|
| `SubmitReturnRequest` | Request | `Guid OrderId`, `ReturnItemDto[] Items`, `string Type` (Return/Exchange), `string Reason` |
| `ReturnItemDto` | Request/Response | `Guid OrderLineId`, `int Quantity`, `string? ExchangeVariantId` |
| `ReturnRequestDto` | Response | `Guid Id`, `Guid OrderId`, `string Status`, `string Type`, `ReturnItemDto[] Items`, `DateTime SubmittedAtUtc` |
| `RejectReturnRequest` | Request | `string Reason` |

### Shared Value DTOs

| DTO | Fields |
|-----|--------|
| `MoneyDto` | `decimal Amount`, `string Currency` |
| `AddressDto` | `string Street`, `string City`, `string State`, `string ZipCode`, `string CountryCode` |
| `ProblemDetails` | `string Type`, `string Title`, `int Status`, `string Detail`, `string Instance`, `Dictionary<string,string[]>? Errors` |

---

## Middleware Pipeline State Model

| Stage | Component | State Written | State Read |
|-------|-----------|---------------|------------|
| 1 | `GlobalExceptionHandler` | `ProblemDetails` response body | Caught `Exception` |
| 2 | `SecurityHeadersMiddleware` | Response headers | — |
| 3 | `CorrelationIdMiddleware` | `HttpContext.Items["CorrelationId"]`, `X-Correlation-ID` response header | `X-Correlation-ID` request header (or generates GUID) |
| 4 | `SerilogRequestLogging` | Structured log event | CorrelationId, route template, status code |
| 5 | Response Compression | Compressed response body | `Accept-Encoding` header |
| 6 | CORS | CORS response headers | `Origin` request header; allowed origins from `VendorRuntimeConfig` |
| 7 | Rate Limiter | Lease tracking in memory | Named policy per endpoint group; client IP / user identity |
| 8 | `MaintenanceModeMiddleware` | 503 response for non-exempt routes | `VendorRuntimeConfig.FeatureFlags["maintenanceMode"]` |
| 9 | Auth/Authz | `ClaimsPrincipal` on `HttpContext.User` | JWT Bearer token (Authorization header or `access_token` query param for SignalR) |

---

## API Version Strategy

| Version | Status | Routes | Sunset Date |
|---------|--------|--------|--------------|
| 1.0 | Current | `/api/v1/...` | — |

---

## Rate Limit Policy State

| Policy | Window | Limit | HTTP 429 Retry-After |
|--------|--------|-------|----------------------|
| `auth` | 1 minute | 10 req | Yes |
| `catalog` | 1 minute | 300 req | Yes |
| `webhook` | 1 minute | 50 req | Yes |
| `default` | 1 minute | 100 req | Yes |
