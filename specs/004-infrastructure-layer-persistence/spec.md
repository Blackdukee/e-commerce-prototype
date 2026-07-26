# Feature Specification: Infrastructure Layer & Persistence

**Feature**: 004-infrastructure-layer-persistence  
**Created**: 2026-07-25  
**Status**: Draft  

---

## Executive Summary

The Infrastructure Layer (`Vendor.Infrastructure`) provides production-ready implementations for every interface declared in `Vendor.Domain` (10 repositories, 6 adapters) and `Vendor.Application` (7 core application services). It establishes relational persistence using EF Core against MSSQL, a transactional outbox background dispatcher, multi-provider payment and shipping integrations (Stripe, PayPal, Paymob, Shippo, FlatRate), JWT/OAuth auth providers, a dual-mode cache (Memory vs Redis with SignalR backplane), real-time admin SignalR notifications, dual-mode email senders (SendGrid vs SMTP), and consent-gated analytics flushing.

---

## User Scenarios & Acceptance Criteria

### Scenario 1: Relational Persistence with Value Objects and Soft Delete

**Given** a multi-tenant e-commerce system running against MSSQL,  
**When** aggregates containing value objects (`Money`, `Address`), collections (`Tags`, `Categories`, variant `Attributes`), or soft-delete flags are saved,  
**Then**:
- `Money` and `Address` are persisted as owned entity columns directly on the aggregate table (zero separate lookup tables).
- Primitive arrays and dictionaries are serialized to MSSQL `nvarchar(max)` JSON columns.
- Soft-deleted `Product` and `Customer` records set `IsDeleted = true` and are automatically filtered out by global EF Core query filters.
- Unique constraints on `Slug`, `Sku`, `Email`, and `OrderNumber` prevent duplicate insertions.
- Transient SQL database failures automatically trigger connection retries (`EnableRetryOnFailure`).

### Scenario 2: Transactional Outbox Event Dispatching

**Given** an application command that mutates domain aggregates and raises domain events,  
**When** the command saves changes within a database transaction,  
**Then**:
- Outbox event rows are written into the `OutboxMessages` table in the exact same DB transaction.
- A background worker polls `OutboxMessages` every 2 seconds and processes unprocessed events in batches of up to 20.
- Failed event publishing retries up to 3 times before moving the record to a dead-letter state.

### Scenario 3: Multi-Provider Payment Gateway Processing

**Given** a vendor configured with active payment providers (Stripe, PayPal, Paymob),  
**When** a payment authorization, capture, or refund request is submitted,  
**Then**:
- `PaymentGatewayFactory` resolves the requested payment provider at runtime.
- Every payment request forwards a unique idempotency key to the external payment API.
- Incoming webhooks are cryptographically validated:
  - **Stripe**: HMAC SHA-256 verification of the `Stripe-Signature` header.
  - **PayPal**: Verification via PayPal's `verify-webhook-signature` REST endpoint.
  - **Paymob**: HMAC SHA-512 calculation over lexicographically sorted response payload fields.

### Scenario 4: Real-Time Admin Notifications & Backplane

**Given** administrative users connected to `AdminNotificationHub` at `/hubs/admin`,  
**When** domain events occur (e.g. `OrderPlaced`, `PaymentFailed`, `ProductLowStock`),  
**Then**:
- `IRealtimeNotifier` dispatches real-time typed events (`OnNewOrder`, `OnPaymentReceived`, `OnPaymentFailed`, `OnLowStock`, `OnOrderCancelled`, `OnReturnRequested`, `OnShipmentDelivered`, `OnSettingsUpdated`).
- Hub connections authenticate via `access_token` query string parameter.
- If Redis caching is enabled, Redis automatically serves as the SignalR backplane for multi-instance horizontal scaling.

### Scenario 5: Consent-Gated Analytics Flushing

**Given** analytics events captured during customer browsing sessions,  
**When** customers have granted analytics consent (`AnalyticsConsent == true`),  
**Then**:
- Events are buffered in an in-memory thread-safe queue.
- A background worker flushes buffered events every 30 seconds to GA4 Measurement Protocol and/or configured HTTP webhooks.
- Events from customers who denied consent are discarded at capture time.

---

## Technical Functional Requirements

### 1. Persistence & EF Core Mapping (`Vendor.Infrastructure/Persistence/`)
- Implement `VendorDbContext` inheriting `DbContext` and `IUnitOfWork`.
- Configure owned entity types for `Money` (`Amount`, `Currency`) and `Address` (`Street`, `City`, `State`, `ZipCode`, `CountryCode`).
- Use EF Core JSON columns (`HasConversion` / `ToJson()`) for primitive lists and dictionaries (`Product.Images`, `ProductVariant.Attributes`, `Promotion.TemplateIds`).
- Apply `HasQueryFilter(e => !e.IsDeleted)` for `Product` and `Customer`.
- Define unique indexes: `IX_Products_Slug`, `IX_ProductVariants_Sku`, `IX_Customers_Email`, `IX_Orders_OrderNumber`.
- Enable EF Core SQL Server resilient execution strategy (`EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)`).

### 2. Transactional Outbox (`Vendor.Infrastructure/Outbox/`)
- `OutboxMessage` entity schema: `Id` (Guid), `Type` (string), `Content` (nvarchar(max) JSON), `OccurredOnUtc` (datetime2), `ProcessedOnUtc` (datetime2?), `Error` (nvarchar(max)?), `RetryCount` (int).
- `OutboxInterceptor` intercepts `SaveChangesAsync` to convert raised aggregate `IDomainEvent` instances into `OutboxMessage` rows.
- `OutboxProcessorHostedService` runs background loop every 2 seconds, reading up to 20 unprocessed messages ordered by `OccurredOnUtc`, publishing via MediatR `IPublisher`, updating `ProcessedOnUtc` or incrementing `RetryCount` up to 3 max retries before setting dead-letter error status.

### 3. Payment Gateway Adapters (`Vendor.Infrastructure/Payments/`)
- `StripePaymentGateway`: Uses `StripeClient` / PaymentIntents API with secret key reference; verifies webhooks via `EventUtility.ConstructEvent` (HMAC SHA-256).
- `PayPalPaymentGateway`: Uses PayPal REST API v2 OAuth2 client credentials (`/v1/oauth2/token`); supports payment creation & capture; verifies webhooks via `/v1/notifications/verify-webhook-signature`.
- `PaymobPaymentGateway`: Performs 3-step auth flow (Auth Token -> Order Registration -> Payment Key generation); returns iframe URL; validates webhooks via HMAC SHA-512 over sorted parameters (`amount_cents`, `created_at`, `currency`, `error_occured`, `has_parent_transaction`, `id`, `integration_id`, `is_3d_secure`, `is_auth`, `is_capture`, `is_refunded`, `is_standalone_payment`, `order.id`, `owner`, `pending`, `source_data.pan`, `source_data.sub_type`, `source_data.type`, `success`).
- `PaymentGatewayFactory`: Implements `IPaymentGatewayFactory` resolving target adapter based on vendor runtime configuration.

### 4. Shipping Adapters (`Vendor.Infrastructure/Shipping/`)
- `FlatRateShippingProvider`: Implements `IShippingProvider` with fixed rates and thresholds from vendor runtime configuration.
- `ShippoShippingProvider`: Implements `IShippingProvider` connecting to Shippo REST API for live rate quotes, shipping label generation, and tracking info.

### 5. Authentication Services (`Vendor.Infrastructure/Auth/`)
- `JwtTokenService`: Implements `ITokenService` issuing HMAC-SHA256 JWT access tokens (30-min lifetime) containing `sub`, `email`, `role` claims, and cryptographically secure random 64-byte refresh tokens (7-day lifetime, persisted in DB `RefreshTokens` table, invalidated/rotated upon use).
- `ExternalAuthService`: Implements `IExternalAuthService` calling Google `https://oauth2.googleapis.com/tokeninfo?id_token={token}` and Facebook `https://graph.facebook.com/me?fields=id,email,first_name,last_name&access_token={token}`.

### 6. Caching & Real-time Services (`Vendor.Infrastructure/Caching/`, `/Realtime/`)
- Single config key `Caching:Provider` (`Memory` vs `Redis`).
- `InMemoryCacheService` uses `IMemoryCache`; `RedisCacheService` uses `IDistributedCache` / StackExchange.Redis.
- When `Redis` is active, auto-configure SignalR Redis backplane (`AddSignalR().AddStackExchangeRedis(...)`).
- Domain event handlers invalidate cached keys on mutations: `ProductUpdatedEvent` -> invalidate `products:listings`, `products:slug:{slug}`; `PromotionUpdatedEvent` -> invalidate `promotions:active`.
- `AdminNotificationHub`: SignalR hub at `/hubs/admin` implementing `IAdminNotificationClient` (8 typed methods). `SignalRRealtimeNotifier` implements `IRealtimeNotifier` and dispatches hub messages.

### 7. Email & Analytics Services (`Vendor.Infrastructure/Email/`, `/Analytics/`)
- Single config key `Email:Provider` (`SendGrid` vs `Smtp`).
- `SendGridEmailSender` uses SendGrid API SDK; `SmtpEmailSender` uses MailKit SMTP client. Supports 4 template types: Order Confirmation, Shipping Update, Password Reset, Email Verification.
- `AnalyticsProcessorHostedService`: Thread-safe `Channel<AnalyticsEvent>` background queue flushing consent-gated events every 30 seconds in batches to GA4 Measurement Protocol (`https://www.google-analytics.com/mp/collect`) and/or vendor-configured HTTP webhook.

---

## Success Criteria

1. **Persistence Verification**: 100% of Domain aggregates persist to MSSQL without separate value object tables or data loss.
2. **Outbox Guarantee**: Domain events generated by aggregate changes are reliably saved in outbox rows and dispatched to handlers within 2 seconds.
3. **Webhook Security**: 100% of simulated Stripe, PayPal, and Paymob webhooks pass signature validation; invalid signatures return HTTP 400/401.
4. **Idempotent Payments**: All payment gateway requests carry non-empty idempotency keys.
5. **Real-time Dispatch**: Admin notification hub emits typed events across all 8 operational methods on corresponding domain events.
