# Contract: Payment Webhook Signature Validation Protocols

**Feature**: 004-infrastructure-layer-persistence  

---

## 1. Stripe Webhook Verification

- **Header**: `Stripe-Signature`
- **Algorithm**: HMAC SHA-256
- **Verification Method**:
  ```csharp
  var stripeEvent = EventUtility.ConstructEvent(
      jsonPayload,
      stripeSignatureHeader,
      webhookSecret,
      throwOnApiVersionMismatch: false);
  ```
- **Failure Status**: `400 Bad Request` if `StripeException` thrown.

---

## 2. PayPal Webhook Verification

- **Endpoint**: `POST https://api-m.paypal.com/v1/notifications/verify-webhook-signature`
- **Verification Payload**:
  ```json
  {
    "auth_algo": "<PAYPAL-AUTH-ALGO-HEADER>",
    "cert_url": "<PAYPAL-CERT-URL-HEADER>",
    "transmission_id": "<PAYPAL-TRANSMISSION-ID-HEADER>",
    "transmission_sig": "<PAYPAL-TRANSMISSION-SIG-HEADER>",
    "transmission_time": "<PAYPAL-TRANSMISSION-TIME-HEADER>",
    "webhook_id": "<CONFIGURED_WEBHOOK_ID>",
    "webhook_event": <RAW_JSON_BODY_OBJECT>
  }
  ```
- **Validation Check**: Response contains `"verification_status": "SUCCESS"`.
- **Failure Status**: `400 Bad Request` if status is `"FAILURE"`.

---

## 3. Paymob Webhook Verification

- **Algorithm**: HMAC SHA-512
- **Verification Parameters (Lexicographical Order)**:
  `amount_cents`, `created_at`, `currency`, `error_occured`, `has_parent_transaction`, `id`, `integration_id`, `is_3d_secure`, `is_auth`, `is_capture`, `is_refunded`, `is_standalone_payment`, `order.id`, `owner`, `pending`, `source_data.pan`, `source_data.sub_type`, `source_data.type`, `success`
- **Verification Method**:
  Concat values of above sorted keys into string -> compute HMAC SHA-512 hash using vendor's HMAC secret -> compare against `hmac` query parameter / body field.
- **Failure Status**: `400 Bad Request` if calculated hash does not match `hmac`.
