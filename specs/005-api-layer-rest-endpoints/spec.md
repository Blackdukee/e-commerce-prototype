# Feature Specification: API Layer Composition Root & REST Endpoints

**Feature Directory**: `specs/005-api-layer-rest-endpoints`  
**Created**: 2026-07-25  
**Status**: SPECIFIED  

---

## Executive Summary

The `Vendor.Api` project serves as the composition root and public HTTP/WebSocket entrance for the multi-tenant e-commerce system. It wires together `Vendor.Domain`, `Vendor.Application`, and `Vendor.Infrastructure` into an ASP.NET Core web application targeting .NET 9.

The API layer exposes **63 REST endpoints** across 9 functional modules (Auth, Products, Cart, Orders, Payments & Webhooks, Shipments, Returns, Promotions, Analytics/Settings/Customers), a real-time SignalR WebSocket hub (`/hubs/admin`), 2 health check endpoints (`/health/live`, `/health/ready`), URL-based API versioning (`/api/v1/...`), 4 rate-limiting policies, structured logging, global ProblemDetails exception handling, and a 9-stage ordered HTTP middleware pipeline with maintenance mode support.

---

## User Stories & Functional Requirements

### User Story 1: API Composition Root & Ordered Middleware Pipeline (Priority: P1) 🎯 MVP

As a system developer or DevOps engineer, I want the ASP.NET Core host to bootstrap the application with ordered middleware and service registrations so that all HTTP requests are handled securely, predictably, and with structured telemetry.

#### Acceptance Criteria
1. **Startup Initialization**: Reads configuration from `vendor.config.json` merged with environment variables and registers Serilog (Console + Seq), Application services (MediatR handlers, FluentValidation validators), and Infrastructure services (EF Core DbContext, Outbox, Adapters).
2. **API Versioning**: URL-segment versioning configured under `/api/v1/...` defaulting to API version `1.0`.
3. **OpenAPI / Swagger**: Auto-generates OpenAPI v3 documentation at `/swagger` with JWT Bearer security scheme definitions and endpoint metadata.
4. **Ordered Middleware Pipeline**: Enforces request execution through 9 stages in exact sequence:
   - Stage 1: Global Exception Handler (translates unhandled exceptions into RFC 7807 `ProblemDetails`).
   - Stage 2: Security Headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy`, `Referrer-Policy: strict-origin-when-cross-origin`).
   - Stage 3: Correlation ID Propagation (`X-Correlation-ID` extracted from incoming header or generated as GUID and attached to response and logging context).
   - Stage 4: Structured Request Logging (Serilog HTTP request logging with route, status code, user context, and duration).
   - Stage 5: Response Compression (Brotli + Gzip compression enabled for JSON and text responses).
   - Stage 6: CORS (configured with allowed origins and `AllowCredentials()` enabled for SignalR websockets).
   - Stage 7: Rate Limiting (enforces 4 named rate limit policies).
   - Stage 8: Maintenance Mode Middleware (evaluates runtime feature flag; if `maintenanceMode == true`, short-circuits with `503 Service Unavailable` for all routes except `/health/*` and `/api/v1/admin/*`).
   - Stage 9: Authentication & Authorization (evaluates JWT Bearer tokens and role policies).

---

### User Story 2: Auth, Customer Profile & Account Endpoints (Priority: P1)

As a customer or guest user, I want dedicated authentication and account management endpoints so that I can register, log in, manage my profile/addresses, grant/revoke analytics consent, convert from guest to registered customer, and securely handle password resets.

#### Acceptance Criteria
1. **Auth Endpoints (9 total)**:
   - `POST /api/v1/auth/register` (Registers customer account)
   - `POST /api/v1/auth/login` (Returns access token + refresh token)
   - `POST /api/v1/auth/guest` (Creates anonymous guest session)
   - `POST /api/v1/auth/refresh` (Rotates refresh token for new access token)
   - `POST /api/v1/auth/revoke` (Revokes active refresh token)
   - `POST /api/v1/auth/external/google` (OAuth2 token verification via Google)
   - `POST /api/v1/auth/external/facebook` (OAuth2 token verification via Facebook Graph API)
   - `POST /api/v1/auth/forgot-password` (Sends password reset token email)
   - `POST /api/v1/auth/reset-password` (Resets password using reset token)
2. **Customer & Consent Endpoints (4 of 11 group)**:
   - `GET /api/v1/customer/profile` (Get current customer profile)
   - `PUT /api/v1/customer/addresses` (Add or update shipping addresses)
   - `PUT /api/v1/customer/consent` (Update analytics consent preference)
   - `POST /api/v1/customer/convert-guest` (Convert guest customer to registered user)

---

### User Story 3: Product Catalog & Administrative Inventory Management Endpoints (Priority: P1)

As a shopper, I want to browse products, filter listings, and view product details by slug; as a store admin, I want full CRUD endpoints to manage products, variants, stock levels, images, and activation status.

#### Acceptance Criteria
1. **Products Endpoints (13 total)**:
   - `GET /api/v1/products` (Public list with pagination, category, tag, search filtering)
   - `GET /api/v1/products/{id}` (Public get product by ID)
   - `GET /api/v1/products/slug/{slug}` (Public get product by Slug)
   - `POST /api/v1/admin/products` (Admin create product)
   - `PUT /api/v1/admin/products/{id}` (Admin update product details)
   - `PUT /api/v1/admin/products/{id}/stock` (Admin adjust variant stock)
   - `POST /api/v1/admin/products/{id}/activate` (Admin activate product)
   - `POST /api/v1/admin/products/{id}/deactivate` (Admin deactivate product)
   - `DELETE /api/v1/admin/products/{id}` (Admin soft-delete product)
   - `POST /api/v1/admin/products/{id}/variants` (Admin create variant)
   - `PUT /api/v1/admin/products/{id}/variants/{variantId}` (Admin update variant)
   - `POST /api/v1/admin/products/{id}/images` (Admin upload product image)
   - `DELETE /api/v1/admin/products/{id}/images` (Admin remove product image)

---

### User Story 4: Shopping Cart & Checkout Endpoints (Priority: P1)

As a buyer, I want REST endpoints to manage my cart items, apply discount codes, merge my guest cart upon login, and initiate two-phase checkout into an order.

#### Acceptance Criteria
1. **Cart Endpoints (7 total)**:
   - `GET /api/v1/cart` (Get active customer or session cart)
   - `POST /api/v1/cart/items` (Add product variant to cart)
   - `PUT /api/v1/cart/items/{variantId}` (Update item quantity)
   - `DELETE /api/v1/cart/items/{variantId}` (Remove item from cart)
   - `POST /api/v1/cart/discounts` (Apply promotion discount code)
   - `DELETE /api/v1/cart/discounts/{code}` (Remove applied discount code)
   - `POST /api/v1/cart/merge` (Merge guest session cart into logged-in user cart)
2. **Checkout Endpoint**:
   - `POST /api/v1/orders/checkout` (Initiates 2-phase checkout orchestrator returning order summary + payment initialization details)

---

### User Story 5: Orders, Payments, Webhooks, Shipments & Returns Endpoints (Priority: P2)

As a customer or store manager, I want REST endpoints to track order status, process payments, verify incoming webhooks, manage shipments, and handle return/exchange workflows.

#### Acceptance Criteria
1. **Orders Endpoints (8 remaining of 9 group)**:
   - `GET /api/v1/orders/my-orders` (Customer list order history)
   - `GET /api/v1/orders/{id}` (Get order details by ID)
   - `GET /api/v1/orders/number/{orderNumber}` (Get order by order number)
   - `POST /api/v1/orders/{id}/cancel` (Cancel order)
   - `POST /api/v1/orders/{id}/refund-request` (Request order refund)
   - `GET /api/v1/admin/orders` (Admin list all orders with status filter)
   - `POST /api/v1/admin/orders/{id}/process` (Admin transition order to processing)
   - `POST /api/v1/admin/orders/{id}/notes` (Admin append internal order note)
2. **Payments & Webhooks Endpoints (8 total)**:
   - `GET /api/v1/payments/{id}` (Get payment details)
   - `GET /api/v1/payments/order/{orderId}` (Get payment by order ID)
   - `POST /api/v1/admin/payments/{id}/capture` (Admin capture payment)
   - `POST /api/v1/admin/payments/{id}/refund` (Admin refund payment)
   - `POST /api/v1/webhooks/stripe` (Stripe HMAC SHA-256 webhook receiver)
   - `POST /api/v1/webhooks/paypal` (PayPal REST webhook receiver)
   - `POST /api/v1/webhooks/paymob` (Paymob HMAC SHA-512 webhook receiver)
   - `POST /api/v1/webhooks/shipping` (Carrier shipping status webhook receiver)
3. **Shipments Endpoints (6 total)**:
   - `POST /api/v1/shipments/rates` (Calculate shipping rates)
   - `GET /api/v1/shipments/track/{trackingNumber}` (Track shipment status)
   - `POST /api/v1/admin/shipments` (Admin create shipment)
   - `POST /api/v1/admin/shipments/{id}/label` (Admin generate shipping label)
   - `POST /api/v1/admin/shipments/{id}/ship` (Admin mark shipped)
   - `POST /api/v1/admin/shipments/{id}/deliver` (Admin mark delivered)
4. **Returns Endpoints (8 total)**:
   - `POST /api/v1/returns` (Customer submit return/exchange request)
   - `GET /api/v1/returns/{id}` (Get return request details)
   - `GET /api/v1/admin/returns` (Admin list return requests)
   - `POST /api/v1/admin/returns/{id}/approve` (Admin approve return)
   - `POST /api/v1/admin/returns/{id}/reject` (Admin reject return)
   - `POST /api/v1/admin/returns/{id}/items-received` (Admin mark items received)
   - `POST /api/v1/admin/returns/{id}/complete-return` (Admin complete refund return)
   - `POST /api/v1/admin/returns/{id}/complete-exchange` (Admin complete exchange return)

---

### User Story 6: Promotions, Analytics, Admin Settings, SignalR & Health Checks (Priority: P2)

As an administrator or automated system monitor, I want endpoints for promotions management, analytics summaries, runtime configuration patches, real-time SignalR websockets, and health probes so that I can operate and monitor the platform.

#### Acceptance Criteria
1. **Promotions Endpoints (4 total)**:
   - `POST /api/v1/promotions/validate` (Public validate promotion code)
   - `POST /api/v1/admin/promotions` (Admin create promotion)
   - `GET /api/v1/admin/promotions` (Admin list promotions)
   - `POST /api/v1/admin/promotions/{id}/deactivate` (Admin deactivate promotion)
2. **Analytics & Settings Endpoints (7 remaining of 11 group)**:
   - `GET /api/v1/admin/analytics/summary` (Admin analytics performance summary)
   - `GET /api/v1/admin/settings` (Admin get runtime configuration)
   - `PATCH /api/v1/admin/settings/branding` (Admin update branding settings)
   - `PATCH /api/v1/admin/settings/checkout` (Admin update checkout settings)
   - `PATCH /api/v1/admin/settings/shipping` (Admin update shipping settings)
   - `PATCH /api/v1/admin/settings/feature-flags` (Admin toggle feature flags)
   - `POST /api/v1/admin/settings/maintenance` (Admin toggle maintenance mode)
3. **SignalR Endpoint**:
   - `/hubs/admin` (WebSocket connection endpoint for real-time admin events authenticated via `access_token` query parameter)
4. **Health Check Probes (2 total)**:
   - `GET /health/live` (Liveness probe — returns 200 OK if process is running)
   - `GET /health/ready` (Readiness probe — verifies MSSQL database connectivity, Redis cache availability, and payment gateway configuration)

---

## 4 Rate Limiting Policies

| Policy Name | Rate Limit Threshold | Target Routes / Scope |
|-------------|----------------------|-----------------------|
| `auth` | 10 requests / minute | `/api/v1/auth/*` (Login, Register, Password Reset) |
| `catalog` | 300 requests / minute | `/api/v1/products*` (Public product browsing) |
| `webhook` | 50 requests / minute | `/api/v1/webhooks/*` (Stripe, PayPal, Paymob, Shipping) |
| `default` | 100 requests / minute | All other authenticated & administrative routes |

---

## Clarifications

### Session 2026-07-25
- Q: How should CORS allowed origins be configured? → A: Load allowed origins from `VendorRuntimeConfig` with wildcard fallback in Development mode.

---

## Non-Functional Requirements & Security

1. **ProblemDetails Protocol**: Every failed handler (`Result.Failure`) or unhandled exception returns RFC 7807 `ProblemDetails` JSON containing `type`, `title`, `status`, `detail`, `instance`, and `errors` dictionary for validation failures (HTTP 422).
2. **Zero Information Leakage**: Technical exception tracebacks are suppressed in non-development environments; internal errors return clean 500 status with correlation ID.
3. **CORS Security**: CORS origins loaded from `VendorRuntimeConfig` with wildcard fallback in Development; `AllowCredentials()` specifically enabled for SignalR WebSocket negotiation.

---

## Assumptions & Boundaries

- Authentication relies on HS256 JWT tokens issued by `JwtTokenService` with 30-minute access token lifespan and 7-day refresh token lifespan.
- Maintenance mode short-circuits HTTP pipeline at Stage 8 before authentication to prevent non-admin requests from accessing application state while allowing admins to configure the platform.
- Health checks return standard ASP.NET Core `HealthReport` formatted JSON.
