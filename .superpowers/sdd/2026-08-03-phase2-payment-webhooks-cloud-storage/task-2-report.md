# Task 2 Report: Payment Webhooks Signature Verification & Endpoints

**Status**: DONE  
**Completed At**: 2026-08-03  
**Commit**: `feat(webhooks): implement Stripe, PayMob, and PayPal webhook endpoints with signature validation` (`8ab2b3f`)

---

## Executive Summary

Task 2 of Phase 2 has been successfully completed. We have built production-grade payment webhook ingestion for **Stripe**, **PayMob**, and **PayPal** featuring:
1. Provider-specific cryptographic signature verification and payload extraction.
2. Replay-protection deduplication checking `IWebhookEventRepository.ExistsAsync(provider, eventId)`.
3. Outbox event dispatching publishing `OrderPaymentSucceededEvent` / `OrderPaymentFailedEvent` for new webhook events.
4. Minimal API endpoints mapped under `/api/v1/webhooks/{stripe|paymob|paypal}`.
5. End-to-end integration tests verifying signature failure rejection (`400 Bad Request`), successful payload ingestion (`200 OK`), and idempotency replay protection (`200 OK`).

---

## 1. Key Components Implemented

### 1.1 Webhook Parsers (`src/Vendor.Infrastructure/Payments/Webhooks/`)
- **`StripeWebhookParser`**: Performs signature verification using `Stripe.EventUtility.ConstructEvent` or HMAC verification with `STRIPE_WEBHOOK_SECRET`. Extracts `EventId`, `EventType`, `Amount`, `Currency`, and `OrderId` metadata.
- **`PaymobWebhookParser`**: Computes HMAC SHA-512 over the 19 concatenated PayMob transaction fields using `PAYMOB_HMAC_SECRET`. Determines transaction success/failure and parses amount in cents.
- **`PaypalWebhookParser`**: Validates transmission headers against `PAYPAL_WEBHOOK_ID`. Extracts PayPal event type (e.g. `PAYMENT.CAPTURE.COMPLETED`), transaction status, and payment resource amount.
- **`WebhookParserFactory`**: Resolves the appropriate `IWebhookParser` strategy based on the provider string ("Stripe", "PayMob", "PayPal").

### 1.2 MediatR Command & Handler (`src/Vendor.Application/Modules/Payments/ProcessPaymentWebhookCommand.cs`)
- **Signature Validation**: Evaluates signature via `IWebhookParserFactory`. If signature is invalid, logs security audit warning (`Security Warning: Invalid {Provider} webhook signature attempt.`) and returns `Result<bool>.Failure(Error.Failure("Webhook.InvalidSignature", "Invalid signature"))`.
- **Replay Protection**: Checks `IWebhookEventRepository.ExistsAsync(provider, eventId)`. If duplicate, logs info and returns `Result<bool>.Success(true)` without duplicate fulfillment.
- **Persistence & Outbox Dispatching**: Saves new `WebhookEvent` entity to the database and dispatches `OrderPaymentSucceededEvent` or `OrderPaymentFailedEvent` via `IOutboxService`.

### 1.3 Endpoints (`src/Vendor.Api/Endpoints/WebhookEndpoints.cs`)
Mapped three minimal API endpoints under versioned API group `/api/v1/webhooks`:
- `POST /api/v1/webhooks/stripe`
- `POST /api/v1/webhooks/paymob`
- `POST /api/v1/webhooks/paypal`

Registered in `src/Vendor.Api/Extensions/WebApplicationExtensions.cs`.

### 1.4 Outbox Infrastructure (`src/Vendor.Infrastructure/Outbox/OutboxService.cs`)
Implemented `IOutboxService` which saves domain events to the `OutboxMessages` database table for background processing and publishes them via MediatR `IPublisher`.

---

## 2. Integration & Verification

### 2.1 Integration Test Suite (`tests/Vendor.Api.Tests/Integration/WebhookEndpointsTests.cs`)
Created comprehensive integration tests covering:
1. `StripeWebhook_WithInvalidSignature_Returns400BadRequest`: Verified `400 Bad Request` returned on bad signature.
2. `PaymobWebhook_WithInvalidSignature_Returns400BadRequest`: Verified `400 Bad Request` returned on bad HMAC.
3. `PaypalWebhook_WithInvalidSignature_Returns400BadRequest`: Verified `400 Bad Request` returned on bad transmission header.
4. `StripeWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry`: Verified initial ingestion returns `200 OK` and duplicate retries return `200 OK` without throwing errors.
5. `PaymobWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry`: Verified `200 OK` and duplicate replay protection.
6. `PaypalWebhook_WithValidSignature_Returns200OK_And_IdempotentOnRetry`: Verified `200 OK` and duplicate replay protection.

### 2.2 Full Solution Test Suite Execution
Ran `dotnet test Vendor.slnx`:
```
Passed! - Failed: 0, Passed: 75, Skipped: 0, Total: 75 - Vendor.Domain.Tests.dll
Passed! - Failed: 0, Passed: 52, Skipped: 0, Total: 52 - Vendor.Application.Tests.dll
Passed! - Failed: 0, Passed: 44, Skipped: 0, Total: 44 - Vendor.Api.Tests.dll
Passed! - Failed: 0, Passed: 31, Skipped: 0, Total: 31 - Vendor.Infrastructure.Tests.dll

Total: 202 Passed, 0 Failed.
```

---

## 3. Files Created / Modified

- `src/Vendor.Domain/Abstractions/IDomainEvent.cs` (Updated to inherit `MediatR.INotification`)
- `src/Vendor.Domain/Events/PaymentAndShipmentEvents.cs` (Added `OrderPaymentSucceededEvent` & `OrderPaymentFailedEvent`)
- `src/Vendor.Domain/Vendor.Domain.csproj` (Added `MediatR.Contracts` dependency)
- `src/Vendor.Application/Common/Interfaces/IOutboxService.cs` (Created interface)
- `src/Vendor.Application/Common/Interfaces/IWebhookParserFactory.cs` (Created interface & record)
- `src/Vendor.Application/Modules/Payments/ProcessPaymentWebhookCommand.cs` (Created command & handler)
- `src/Vendor.Infrastructure/Outbox/OutboxService.cs` (Created Outbox implementation)
- `src/Vendor.Infrastructure/Payments/Webhooks/IWebhookParser.cs` (Created parser interface)
- `src/Vendor.Infrastructure/Payments/Webhooks/StripeWebhookParser.cs` (Created Stripe parser)
- `src/Vendor.Infrastructure/Payments/Webhooks/PaymobWebhookParser.cs` (Created PayMob parser)
- `src/Vendor.Infrastructure/Payments/Webhooks/PaypalWebhookParser.cs` (Created PayPal parser)
- `src/Vendor.Infrastructure/Payments/Webhooks/WebhookParserFactory.cs` (Created factory)
- `src/Vendor.Infrastructure/DependencyInjection.cs` (Registered Webhook & Outbox services)
- `src/Vendor.Api/Endpoints/WebhookEndpoints.cs` (Created webhook endpoints)
- `src/Vendor.Api/Endpoints/PaymentEndpoints.cs` (Removed legacy webhook handler)
- `src/Vendor.Api/Extensions/WebApplicationExtensions.cs` (Registered webhook endpoints)
- `tests/Vendor.Api.Tests/Integration/WebhookEndpointsTests.cs` (Created integration tests)
- `tests/Vendor.Api.Tests/Payments/WebhookIngestionTests.cs` (Updated to expect 400 Bad Request)
