# API Contract: Admin Configuration Endpoints

**Feature**: 001-vendor-config-system
**Date**: 2026-07-25
**Base Path**: `/api/v1/admin/config`

## Authentication

All endpoints require `Authorization: Bearer <token>` with a JWT containing the `VendorAdmin` role claim.

## Endpoints

### GET /api/v1/admin/config

Retrieve the full vendor configuration across all tiers.

**Response**: `200 OK`

```json
{
  "vendorId": "acme-store",
  "vendorDisplayName": "ACME Store",
  "tiers": {
    "build": {
      "vendorId": "acme-store"
    },
    "boot": {
      "auth": {
        "tokenLifetimeMinutes": 60,
        "refreshTokenLifetimeDays": 30,
        "jwtSecret": "ref:***",
        "googleClientId": "123456.apps.googleusercontent.com",
        "googleClientSecret": "ref:***",
        "passwordMinLength": 8,
        "passwordRequireUppercase": true,
        "passwordRequireDigit": true,
        "passwordRequireSpecialChar": false
      },
      "caching": {
        "provider": "Memory",
        "keyPrefix": "acme"
      },
      "email": {
        "provider": "SendGrid",
        "senderAddress": "noreply@acme-store.com",
        "senderName": "ACME Store",
        "sendGridApiKey": "ref:***"
      },
      "analytics": {
        "provider": "ga4",
        "trackingId": "G-XXXXXXXXXX",
        "serverSideForwarding": false,
        "consentRequired": true
      }
    },
    "runtime": {
      "branding": {
        "logoUrl": "https://cdn.acme-store.com/logo.svg",
        "primaryColor": "#2563EB",
        "secondaryColor": "#1E40AF",
        "fontFamily": "Inter",
        "metaTitle": "ACME Store — Quality Products",
        "metaDescription": "Shop the best products at ACME Store"
      },
      "locale": {
        "defaultLanguage": "en",
        "supportedLanguages": ["en", "ar"],
        "defaultCurrency": "USD",
        "supportedCurrencies": ["USD", "EUR"],
        "timezone": "America/New_York",
        "direction": "ltr"
      },
      "tax": {
        "strategy": "Flat",
        "flatRatePercentage": 8.875,
        "pricesIncludeTax": false
      },
      "checkout": {
        "guestCheckoutEnabled": true,
        "maxItemsPerOrder": 50,
        "orderNumberPrefix": "ACM"
      },
      "payments": [
        {
          "providerName": "stripe",
          "enabled": true,
          "isDefault": true,
          "credentials": {
            "publicKey": "pk_live_...",
            "secretKey": "ref:***"
          },
          "supportedMethods": ["card", "apple_pay"],
          "captureMode": "Automatic",
          "webhookSecret": "ref:***"
        }
      ],
      "shipping": [
        {
          "providerName": "flat-rate",
          "enabled": true,
          "settings": {
            "baseRate": 5.99,
            "freeShippingThreshold": 50.00
          }
        }
      ],
      "promotions": {
        "enabled": true,
        "maxDiscountCodesPerOrder": 1,
        "evaluationStrategy": "best-discount",
        "allowStacking": false
      },
      "featureFlags": {
        "enableReviews": true,
        "enableWishlist": false,
        "enableAnalytics": true,
        "maintenanceMode": false,
        "enablePromotions": true
      }
    }
  },
  "version": 3,
  "lastModifiedUtc": "2026-07-25T12:00:00Z"
}
```

**Notes**:
- All `SecretReference` fields are masked as `"ref:***"` in the response.
- The `version` field is the optimistic concurrency token from the DB.

### PATCH /api/v1/admin/config

Update runtime-tier configuration settings. Only runtime-tier fields are accepted.

**Request**: `application/json`

Patch body uses JSON Merge Patch (RFC 7386). Only include fields to update:

```json
{
  "runtime": {
    "branding": {
      "primaryColor": "#DC2626"
    },
    "checkout": {
      "maxItemsPerOrder": 100
    },
    "featureFlags": {
      "enableWishlist": true
    }
  },
  "version": 3
}
```

**Response**: `200 OK` — returns the full updated configuration (same schema as GET).

**Error Responses**:

| Status | Condition | Body |
|--------|-----------|------|
| `400 Bad Request` | Validation failure (schema or business rules) | `{ "errors": [{ "field": "locale.defaultCurrency", "message": "Default currency must be in supported currencies list" }] }` |
| `400 Bad Request` | Attempt to modify build-time or boot-time fields | `{ "errors": [{ "field": "boot.caching.provider", "message": "Boot-time configuration is immutable at runtime" }] }` |
| `409 Conflict` | Version mismatch (optimistic concurrency) | `{ "error": "Configuration was modified by another request. Current version: 4" }` |
| `401 Unauthorized` | Missing or invalid token | Standard 401 |
| `403 Forbidden` | Token lacks VendorAdmin role | Standard 403 |

### GET /api/v1/admin/config/schema

Retrieve the JSON Schema for `vendor.config.json`. Used by admin UIs for client-side validation.

**Response**: `200 OK` — returns `application/schema+json` content.
