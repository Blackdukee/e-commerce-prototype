# Design Document — Phase 2: Payment Webhooks & Cloud Storage Pipeline

**Feature**: Phase 2 Payment Webhooks & Cloud Storage Pipeline
**Date**: 2026-08-03
**Status**: Approved

## 1. Executive Summary

Phase 2 builds production-grade payment webhook ingestion for Stripe, PayMob, and PayPal with cryptographic signature verification, database-backed replay protection, and outbox event dispatching. Additionally, it implements a hybrid cloud file storage abstraction (`IFileStorageService`) backing AWS S3 with an automatic local filesystem fallback for development.

---

## 2. Architecture & Subsystems

### 2.1 Payment Webhook Engine (Stripe, PayMob, & PayPal)

#### Endpoints (`src/Vendor.Api/Endpoints/WebhookEndpoints.cs`)
- `POST /api/v1/webhooks/stripe`
- `POST /api/v1/webhooks/paymob`
- `POST /api/v1/webhooks/paypal`

#### Cryptographic Signature Verification
1. **Stripe**: Evaluates `Stripe-Signature` HTTP header against raw payload bytes using `Stripe.EventUtility.ConstructEvent(rawBody, signatureHeader, stripeWebhookSecret)`.
2. **PayMob**: Calculates HMAC SHA-512 over payload parameters (`amount_cents`, `created_at`, `currency`, `error_occured`, `has_parent_transaction`, `id`, `integration_id`, `is_3d_secure`, `is_auth`, `is_capture`, `is_refunded`, `is_standalone_payment`, `order.id`, `owner`, `pending`, `source_data.pan`, `source_data.sub_type`, `source_data.type`, `success`) using `PAYMOB_HMAC_SECRET`.
3. **PayPal**: Validates PayPal transmission headers (`PAYPAL-TRANSMISSION-ID`, `PAYPAL-TRANSMISSION-SIG`, `PAYPAL-TRANSMISSION-TIME`, `PAYPAL-AUTH-ALGO`, `PAYPAL-CERT-URL`) using `PAYPAL_WEBHOOK_ID`.
4. **Failure Behavior**: If verification fails for any provider, logs a security warning (`Security Warning: Invalid {provider} webhook signature attempt from IP {ip}`) and immediately returns `400 Bad Request`.

#### Database Replay Protection (`WebhookEvents` Entity & Table)
- **Entity**: `WebhookEvent` (`Id`, `Provider`, `EventId`, `EventType`, `ProcessedAtUtc`, `PayloadJson`).
- **Idempotency Check**: Query `await webhookRepository.ExistsAsync(provider, eventId)`.
  - **Duplicate Event**: Returns `200 OK` ("Webhook event already processed") without duplicate order fulfillment.
  - **New Event**: Executes `ProcessPaymentWebhookCommand`, dispatches `OrderPaymentSucceededEvent` / `OrderPaymentFailedEvent` via Outbox, and persists `WebhookEvent`.

---

### 2.2 Cloud File Storage Pipeline (`IFileStorageService`)

#### Contract (`Vendor.Application.Common.Interfaces.IFileStorageService`)
```csharp
public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task<string> GeneratePresignedUploadUrlAsync(string fileName, string contentType, TimeSpan expiration, CancellationToken ct = default);
    Task DeleteFileAsync(string fileUrl, CancellationToken ct = default);
}
```

#### Provider Strategy (`HybridFileStorageService`)
- Evaluates `AWS_S3_BUCKET` configuration.
- **AWS S3 Path**: Uses `AWSSDK.S3` for uploading files to S3 bucket and generating presigned HTTP PUT upload URLs.
- **Local Filesystem Fallback**: When AWS credentials are omitted, seamlessly writes files to `wwwroot/uploads` directory and generates local file URLs (`/uploads/filename.png`).

---

## 3. Testing Strategy

1. **Unit Tests (`Vendor.Infrastructure.Tests`)**:
   - Signature validation pass/fail for Stripe, PayMob, and PayPal parsers.
   - `WebhookEvent` database uniqueness & replay rejection tests.
   - `HybridFileStorageService` S3 vs Local fallback strategy tests.
2. **Integration Tests (`Vendor.Api.Tests`)**:
   - End-to-end HTTP webhook requests (`POST /api/v1/webhooks/stripe`, `/paymob`, `/paypal`) verifying 400 rejection on invalid signatures and 200 OK idempotency on duplicate event retries.
   - Presigned upload URL generation endpoint integration tests.
