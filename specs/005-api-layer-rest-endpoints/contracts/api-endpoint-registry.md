# API Endpoint Registry: Vendor.Api v1.0

**Feature**: 005-api-layer-rest-endpoints  
**Date**: 2026-07-25

> All endpoints live under `/api/v1/` prefix (URL-segment versioned). Middleware pipeline applies globally. Rate limit policy, authorization requirement, and request/response shapes are noted per group.

---

## Auth Endpoints — Rate Limit: `auth` (10 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| POST | `/api/v1/auth/register` | No | `RegisterRequest` | 201 `AuthResponse` |
| POST | `/api/v1/auth/login` | No | `LoginRequest` | 200 `AuthResponse` |
| POST | `/api/v1/auth/guest` | No | `GuestSessionRequest` | 200 `AuthResponse` |
| POST | `/api/v1/auth/refresh` | No | `RefreshTokenRequest` | 200 `AuthResponse` |
| POST | `/api/v1/auth/revoke` | Bearer | `RevokeTokenRequest` | 204 No Content |
| POST | `/api/v1/auth/external/google` | No | `ExternalAuthRequest` | 200 `AuthResponse` |
| POST | `/api/v1/auth/external/facebook` | No | `ExternalAuthRequest` | 200 `AuthResponse` |
| POST | `/api/v1/auth/forgot-password` | No | `ForgotPasswordRequest` | 202 Accepted |
| POST | `/api/v1/auth/reset-password` | No | `ResetPasswordRequest` | 204 No Content |

---

## Customer Endpoints — Rate Limit: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| GET | `/api/v1/customer/profile` | Bearer (Customer) | — | 200 `CustomerDto` |
| PUT | `/api/v1/customer/addresses` | Bearer (Customer) | `AddressDto` | 200 `AddressDto[]` |
| PUT | `/api/v1/customer/consent` | Bearer (Customer) | `{ bool granted }` | 204 No Content |
| POST | `/api/v1/customer/convert-guest` | Bearer (Guest) | `{ string email, string password }` | 200 `AuthResponse` |

---

## Product Endpoints — Public: Rate Limit `catalog` (300 req/min) / Admin: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| GET | `/api/v1/products` | No | `ProductListRequest` (query) | 200 `ProductListResponse` |
| GET | `/api/v1/products/{id}` | No | — | 200 `ProductDetailDto` |
| GET | `/api/v1/products/slug/{slug}` | No | — | 200 `ProductDetailDto` |
| POST | `/api/v1/admin/products` | Bearer (Admin) | `CreateProductRequest` | 201 `ProductDetailDto` |
| PUT | `/api/v1/admin/products/{id}` | Bearer (Admin) | `UpdateProductRequest` | 200 `ProductDetailDto` |
| PUT | `/api/v1/admin/products/{id}/stock` | Bearer (Admin) | `AdjustStockRequest` | 204 No Content |
| POST | `/api/v1/admin/products/{id}/activate` | Bearer (Admin) | — | 204 No Content |
| POST | `/api/v1/admin/products/{id}/deactivate` | Bearer (Admin) | — | 204 No Content |
| DELETE | `/api/v1/admin/products/{id}` | Bearer (Admin) | — | 204 No Content |
| POST | `/api/v1/admin/products/{id}/variants` | Bearer (Admin) | `CreateVariantRequest` | 201 `ProductVariantDto` |
| PUT | `/api/v1/admin/products/{id}/variants/{variantId}` | Bearer (Admin) | `CreateVariantRequest` | 200 `ProductVariantDto` |
| POST | `/api/v1/admin/products/{id}/images` | Bearer (Admin) | `IFormFile image` (multipart) | 201 `{ string url }` |
| DELETE | `/api/v1/admin/products/{id}/images` | Bearer (Admin) | `{ string url }` | 204 No Content |

---

## Cart Endpoints — Rate Limit: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| GET | `/api/v1/cart` | Bearer or Session | — | 200 `CartDto` |
| POST | `/api/v1/cart/items` | Bearer or Session | `AddCartItemRequest` | 200 `CartDto` |
| PUT | `/api/v1/cart/items/{variantId}` | Bearer or Session | `UpdateCartItemRequest` | 200 `CartDto` |
| DELETE | `/api/v1/cart/items/{variantId}` | Bearer or Session | — | 200 `CartDto` |
| POST | `/api/v1/cart/discounts` | Bearer or Session | `ApplyDiscountRequest` | 200 `CartDto` |
| DELETE | `/api/v1/cart/discounts/{code}` | Bearer or Session | — | 200 `CartDto` |
| POST | `/api/v1/cart/merge` | Bearer (Customer) | `MergeCartRequest` | 200 `CartDto` |

---

## Order Endpoints — Rate Limit: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| POST | `/api/v1/orders/checkout` | Bearer | `CheckoutRequest` | 201 `CheckoutResponse` |
| GET | `/api/v1/orders/my-orders` | Bearer (Customer) | `{ int page, int pageSize }` (query) | 200 `OrderListResponse` |
| GET | `/api/v1/orders/{id}` | Bearer | — | 200 `OrderDto` |
| GET | `/api/v1/orders/number/{orderNumber}` | Bearer | — | 200 `OrderDto` |
| POST | `/api/v1/orders/{id}/cancel` | Bearer (Customer) | `CancelOrderRequest` | 204 No Content |
| POST | `/api/v1/orders/{id}/refund-request` | Bearer (Customer) | `RefundRequestDto` | 202 Accepted |
| GET | `/api/v1/admin/orders` | Bearer (Admin) | `{ string? status, int page, int pageSize }` (query) | 200 `OrderListResponse` |
| POST | `/api/v1/admin/orders/{id}/process` | Bearer (Admin) | — | 204 No Content |
| POST | `/api/v1/admin/orders/{id}/notes` | Bearer (Admin) | `AddOrderNoteRequest` | 204 No Content |

---

## Payment Endpoints — Admin: `default` / Webhooks: `webhook` (50 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| GET | `/api/v1/payments/{id}` | Bearer | — | 200 `PaymentDto` |
| GET | `/api/v1/payments/order/{orderId}` | Bearer | — | 200 `PaymentDto` |
| POST | `/api/v1/admin/payments/{id}/capture` | Bearer (Admin) | `CapturePaymentRequest` | 200 `PaymentDto` |
| POST | `/api/v1/admin/payments/{id}/refund` | Bearer (Admin) | `RefundPaymentRequest` | 200 `PaymentDto` |
| POST | `/api/v1/webhooks/stripe` | HMAC-SHA256 signature | Raw body | 200 OK |
| POST | `/api/v1/webhooks/paypal` | PayPal signature | Raw body | 200 OK |
| POST | `/api/v1/webhooks/paymob` | HMAC-SHA512 signature | Raw body | 200 OK |
| POST | `/api/v1/webhooks/shipping` | Carrier token | Raw body | 200 OK |

---

## Shipment Endpoints — Rate Limit: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| POST | `/api/v1/shipments/rates` | Bearer | `ShippingRatesRequest` | 200 `ShippingRatesResponse` |
| GET | `/api/v1/shipments/track/{trackingNumber}` | No | — | 200 `TrackingResponse` |
| POST | `/api/v1/admin/shipments` | Bearer (Admin) | `CreateShipmentRequest` | 201 `ShipmentDto` |
| POST | `/api/v1/admin/shipments/{id}/label` | Bearer (Admin) | — | 200 `ShipmentDto` |
| POST | `/api/v1/admin/shipments/{id}/ship` | Bearer (Admin) | — | 204 No Content |
| POST | `/api/v1/admin/shipments/{id}/deliver` | Bearer (Admin) | — | 204 No Content |

---

## Return Endpoints — Rate Limit: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| POST | `/api/v1/returns` | Bearer (Customer) | `SubmitReturnRequest` | 201 `ReturnRequestDto` |
| GET | `/api/v1/returns/{id}` | Bearer | — | 200 `ReturnRequestDto` |
| GET | `/api/v1/admin/returns` | Bearer (Admin) | `{ string? status, int page, int pageSize }` (query) | 200 paginated list |
| POST | `/api/v1/admin/returns/{id}/approve` | Bearer (Admin) | — | 204 No Content |
| POST | `/api/v1/admin/returns/{id}/reject` | Bearer (Admin) | `RejectReturnRequest` | 204 No Content |
| POST | `/api/v1/admin/returns/{id}/items-received` | Bearer (Admin) | — | 204 No Content |
| POST | `/api/v1/admin/returns/{id}/complete-return` | Bearer (Admin) | — | 204 No Content |
| POST | `/api/v1/admin/returns/{id}/complete-exchange` | Bearer (Admin) | — | 204 No Content |

---

## Promotion Endpoints — Rate Limit: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| POST | `/api/v1/promotions/validate` | Bearer or Session | `{ string code }` | 200 `{ bool valid, MoneyDto discount }` |
| POST | `/api/v1/admin/promotions` | Bearer (Admin) | `CreatePromotionRequest` | 201 `PromotionDto` |
| GET | `/api/v1/admin/promotions` | Bearer (Admin) | query params | 200 paginated list |
| POST | `/api/v1/admin/promotions/{id}/deactivate` | Bearer (Admin) | — | 204 No Content |

---

## Analytics & Settings Endpoints — Rate Limit: `default` (100 req/min)

| Method | Route | Auth Required | Request Body | Response |
|--------|-------|---------------|--------------|----------|
| GET | `/api/v1/admin/analytics/summary` | Bearer (Admin) | `{ DateTime from, DateTime to }` (query) | 200 analytics summary |
| GET | `/api/v1/admin/settings` | Bearer (Admin) | — | 200 `VendorRuntimeConfig` |
| PATCH | `/api/v1/admin/settings/branding` | Bearer (Admin) | partial `BrandingConfig` | 200 `BrandingConfig` |
| PATCH | `/api/v1/admin/settings/checkout` | Bearer (Admin) | partial `CheckoutConfig` | 200 `CheckoutConfig` |
| PATCH | `/api/v1/admin/settings/shipping` | Bearer (Admin) | partial `ShippingConfig` | 200 `ShippingConfig` |
| PATCH | `/api/v1/admin/settings/feature-flags` | Bearer (Admin) | `Dictionary<string,bool>` | 200 `FeatureFlags` |
| POST | `/api/v1/admin/settings/maintenance` | Bearer (Admin) | `{ bool enabled }` | 204 No Content |

---

## SignalR & Health Endpoints

| Type | Endpoint | Auth Required | Notes |
|------|----------|---------------|-------|
| WebSocket | `/hubs/admin` | Bearer (Admin) via `access_token` query param | Real-time push notifications to admin dashboard |
| HTTP GET | `/health/live` | No | Liveness probe — always 200 if process running |
| HTTP GET | `/health/ready` | No | Readiness probe — checks MSSQL, Redis, Payment Gateway config |

---

## ProblemDetails Response Schema

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 422,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/v1/auth/register",
  "correlationId": "a3f1b2c4-...",
  "errors": {
    "Email": ["Email is required."],
    "Password": ["Password must be at least 8 characters."]
  }
}
```
