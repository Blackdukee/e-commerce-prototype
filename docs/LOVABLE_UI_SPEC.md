# 🎨 LOVABLE.DEV UI BUILD SPECIFICATION & COMPREHENSIVE PROMPT

> **Project Target**: ACME E-Commerce Platform Prototype (.NET 9 Clean Architecture, Outbox Engine, Egyptian Market EGP / Bosta / 14% VAT)  
> **Purpose**: Complete specification and copy-pasteable build prompt for Lovable.dev to create the full Frontend UI.

---

## 📌 PART 1: MASTER LOVABLE BUILD PROMPT

Copy and paste the single prompt block below directly into Lovable.dev:

```markdown
Build a dual-register E-Commerce Storefront & Vendor Mission Control web application for an Egyptian/MENA multi-vendor retail platform backed by a .NET 9 Clean Architecture API (Outbox Pattern, EF Core, Redis, Hangfire, Elastic/Kibana, Bosta shipping, 14% flat VAT). The system uses two distinct visual registers sharing a single CSS design system: (a) a Storefront designed with warm, high-contrast visual hierarchy tuned for conversion and regional trust, and (b) a Mission Control ops dashboard designed with calm, high-density glassmorphism and clear status indicators for system health, outbox messages, and live events.

### DESIGN SYSTEM & TOKEN ENGINE

```css
:root {
  /* Shared Base & Typography */
  --font-display: 'Outfit', sans-serif;
  --font-body: 'Inter', sans-serif;
  --font-mono: 'JetBrains Mono', monospace;
  --radius-sm: 6px;
  --radius-md: 12px;
  --radius-lg: 18px;

  /* Register A: Storefront (Warm, Rich, High-Contrast Commerce) */
  --sf-bg: #0F172A;
  --sf-surface: #1E293B;
  --sf-border: rgba(255, 255, 255, 0.08);
  --sf-accent: #3B82F6;
  --sf-accent-hover: #2563EB;
  --sf-gold: #F59E0B;
  --sf-text-main: #F8FAFC;
  --sf-text-muted: #94A3B8;

  /* Register B: Mission Control (Frosted Glass, High-Density Telemetry) */
  --mc-bg: #0B0F19;
  --mc-glass-surface: rgba(17, 24, 39, 0.75);
  --mc-border: rgba(255, 255, 255, 0.06);
  --mc-status-ok: #10B981;
  --mc-status-warn: #F59E0B;
  --mc-status-err: #EF4444;
  --mc-status-outbox: #8B5CF6;
  --mc-text-main: #F3F4F6;
  --mc-text-muted: #9CA3AF;
}
```

Support full LTR/RTL layout stubbing via standard `dir="ltr"` / `dir="rtl"` root switching and logical CSS properties (`margin-inline-start`, `border-inline-start`).

---

### PHASE 1 — DESIGN SYSTEM & SHELL ARCHITECTURE

**In Scope:**
- App shell with a top navigation header containing an mode toggle between `🛍️ Customer Storefront` and `📡 Vendor Mission Control`, plus an `RTL (العربية)` direction stub toggle.
- System health status chip in header reading `API 200 OK | Outbox Active | EGP (EGP)`.
- Responsive breakpoints down to 360px mobile viewport, custom focus rings, and `@media (prefers-reduced-motion)` CSS rules.

**Data Fields:**
- `vendorDisplayName`: "ACME Store Egypt"
- `defaultCurrency`: "EGP"
- `flatRatePercentage`: 14.0

**Non-Goals:** Do not connect live backend sockets or build deep database management modals in Phase 1.

---

### PHASE 2 — STOREFRONT CORE EXPERIENCE

**In Scope:**
1. **Home / Product Catalog**:
   - Product Grid displaying cards with `Name`, `Slug`, `BasePrice` (EGP), stock badge, and primary "Add to Order" action.
   - Filtering by category and min/max price sliders.
2. **Product Detail Drawer/Page**:
   - High-res product thumbnail, SKU (`SKU-EGY-1001`), description, and delivery estimate badge ("Delivered in 2-3 business days via Bosta").
3. **Cart & Egyptian Shipping/Tax Calculator**:
   - Cart item list with unit quantity increment/decrement.
   - **Bosta Egyptian Shipping Calculator**:
     - Cairo / Giza: `50.00 EGP`
     - Alexandria: `75.00 EGP`
     - Delta & Canal Governorates: `95.00 EGP`
     - Upper Egypt / Red Sea: `120.00 EGP`
   - **Tax Line Item**: Explicit `14% Flat VAT` line item added before total amount.
   - **Payment Gateway Selector**: Radios for `Credit / Debit Card (Stripe)`, `PayMob Mobile Wallet (Card / Fawry)`, and `PayPal`.
4. **Order Confirmation & Customer Tracking**:
   - Summary showing Order Number (`ACM-2026-8841`), tracking step indicator (`Order Placed` $\rightarrow$ `Payment Verified` $\rightarrow$ `Dispatched via Bosta` $\rightarrow$ `Delivered`), and detailed cost breakdown.

---

### PHASE 3 — MISSION CONTROL DASHBOARD (VENDOR/ADMIN OPERATING SYSTEM)

**In Scope:**
1. **Orders Management Queue**:
   - Table showing `OrderId`, `CustomerEmail`, `TotalAmount` (EGP), `PaymentStatus` (`Paid`, `Pending`, `Failed`), `ShippingCarrier` (`Bosta`), and `CreatedAtUtc`.
   - Domain Event Status Badges: `OrderPlacedEvent`, `OrderPaymentSucceededEvent`, `OrderPaymentFailedEvent`.
2. **Inventory & Products Management**:
   - SKU stock adjustment controls, status toggle (`Active` / `Draft`), and instant inline search.
3. **Promotions & Returns Processing**:
   - Active promotion discount code manager (`evaluationStrategy: best-discount`) and Return Requests approval queue (`Reason`, `RefundAmount`, `Approve`/`Reject` actions).
4. **Vendor Configuration Manager**:
   - Editable fields for `tokenLifetimeMinutes`, `smtpHost`, `bostaApiKey`, `stripeWebhookSecret`, and `paymobHmacSecret`.

---

### PHASE 4 — LIVE TELEMETRY, OUTBOX ENGINE & OPS AUDIT

**In Scope:**
1. **Live Event Ticker & Log Console**:
   - Interactive stream simulating real-time WebSockets/SignalR events from `OutboxProcessorJob`.
   - Displays timestamp, event type (`OrderPlacedEvent`), log level (`INFO`, `WARN`, `ERROR`), and correlation ID (`cid_88f91024a`).
2. **Infrastructure Health Indicators**:
   - Visual status meters for **SQL Server 2022** (`localhost:14330`), **Redis Cache** (`localhost:6379`), **Elasticsearch 8.13** (`localhost:9200`), and **Hangfire Job Scheduler** (`Cron: */5 * * * * *`).
3. **Webhook Replay & Idempotency Audit Panel**:
   - Log table for `WebhookEvents` verifying `Provider` (`Stripe`/`PayMob`/`PayPal`), `EventId`, `ProcessedAtUtc`, and duplicate event replay protection status.
```

---

## 🔌 PART 2: COMPLETE API CONTRACTS & SCHEMAS

Below are the exact JSON request and response payloads for all backend endpoints defined in the solution:

### 1. Authentication Endpoints

#### `POST /api/v1/auth/register`
**Request Payload:**
```json
{
  "email": "customer@acme.eg",
  "firstName": "Tarek",
  "lastName": "Hassan",
  "password": "Password123!"
}
```
**Response Payload (201 Created):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "ref_99a8b12c44e7",
  "expiresAtUtc": "2026-08-08T17:00:00Z",
  "customer": {
    "id": "c7a810f2-8924-4f11-b921-123456789abc",
    "email": "customer@acme.eg",
    "firstName": "Tarek",
    "lastName": "Hassan",
    "customerType": "Standard",
    "analyticsConsent": true
  }
}
```

#### `POST /api/v1/auth/login`
**Request Payload:**
```json
{
  "email": "customer@acme.eg",
  "password": "Password123!"
}
```

#### `GET /api/v1/auth/guest-session`
**Response Payload (200 OK):**
```json
{
  "sessionId": "guest_sess_8832a11b90",
  "createdAtUtc": "2026-08-07T17:00:00Z"
}
```

---

### 2. Product Catalog Endpoints

#### `GET /api/v1/products`
**Response Payload (200 OK):**
```json
[
  {
    "id": "p100-studio-pods",
    "name": "Noise-Cancelling Studio Pods Pro",
    "slug": "studio-pods-pro",
    "basePriceAmount": 2499.00,
    "currency": "EGP",
    "status": "Active",
    "images": [ "https://cdn.acme-store.com/products/pods-pro.jpg" ]
  },
  {
    "id": "p200-mech-keyboard",
    "name": "Custom Mechanical Keyboard RGB",
    "slug": "mech-keyboard-rgb",
    "basePriceAmount": 3850.00,
    "currency": "EGP",
    "status": "Active",
    "images": [ "https://cdn.acme-store.com/products/keyboard.jpg" ]
  }
]
```

#### `GET /api/v1/products/{slug}`
**Response Payload (200 OK):**
```json
{
  "id": "p100-studio-pods",
  "name": "Noise-Cancelling Studio Pods Pro",
  "slug": "studio-pods-pro",
  "description": "High-fidelity active noise cancellation with 36-hour battery life.",
  "basePriceAmount": 2499.00,
  "currency": "EGP",
  "status": "Active",
  "tags": ["audio", "wireless", "premium"],
  "categories": ["Electronics", "Audio"],
  "images": ["https://cdn.acme-store.com/products/pods-pro.jpg"],
  "variants": [
    {
      "id": "v101-black",
      "sku": "SKU-PODS-BLK",
      "stockQuantity": 45,
      "priceAdjustmentAmount": 0.00,
      "currency": "EGP"
    }
  ]
}
```

#### `GET /api/v1/products/search?query=audio`
**Response Payload (200 OK):**
```json
{
  "items": [
    {
      "id": "p100-studio-pods",
      "name": "Noise-Cancelling Studio Pods Pro",
      "slug": "studio-pods-pro",
      "description": "High-fidelity active noise cancellation.",
      "basePrice": 2499.00,
      "currency": "EGP",
      "status": "Active",
      "createdAtUtc": "2026-08-01T12:00:00Z"
    }
  ],
  "totalCount": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

---

### 3. Cart & Checkout Endpoints

#### `GET /api/v1/cart`
**Response Payload (200 OK):**
```json
{
  "id": "cart_88319a02",
  "items": [
    {
      "variantId": "v101-black",
      "productName": "Noise-Cancelling Studio Pods Pro",
      "sku": "SKU-PODS-BLK",
      "quantity": 1,
      "unitPrice": { "amount": 2499.00, "currency": "EGP" },
      "lineTotal": { "amount": 2499.00, "currency": "EGP" }
    }
  ],
  "discountCode": null,
  "subtotal": { "amount": 2499.00, "currency": "EGP" },
  "total": { "amount": 2499.00, "currency": "EGP" }
}
```

#### `POST /api/v1/cart/checkout`
**Request Payload:**
```json
{
  "shippingAddress": {
    "street": "9 El-Tahrir Square",
    "city": "Cairo",
    "state": "Cairo Governorate",
    "zipCode": "11511",
    "countryCode": "EG"
  },
  "shippingServiceCode": "BOSTA_EXPRESS",
  "paymentProvider": "paymob"
}
```
**Response Payload (200 OK):**
```json
{
  "orderId": "b8a910f2-1144-4e22-8811-998877665544",
  "orderNumber": "ACM-2026-8841",
  "total": { "amount": 2898.86, "currency": "EGP" },
  "paymentInit": {
    "provider": "paymob",
    "clientSecret": null,
    "approvalUrl": "https://accept.paymob.com/api/acceptance/iframes/5592983?payment_token=token_abc123",
    "paymentKey": "token_abc123"
  }
}
```

---

### 4. Shipping & Rates Endpoints (Bosta Logistics)

#### `POST /api/v1/shipping/rates`
**Request Payload:**
```json
{
  "origin": { "street": "Vendor Warehouse", "city": "Cairo", "state": "Cairo", "zipCode": "11511", "countryCode": "EG" },
  "destination": { "street": "Corniche El Nile", "city": "Alexandria", "state": "Alexandria", "zipCode": "21500", "countryCode": "EG" },
  "weightKg": 1.5,
  "lengthCm": 20,
  "widthCm": 15,
  "heightCm": 10
}
```
**Response Payload (200 OK):**
```json
[
  {
    "serviceCode": "BOSTA_STANDARD",
    "serviceName": "Bosta Standard Shipping (Egypt)",
    "cost": { "amount": 75.00, "currency": "EGP" },
    "estimatedDaysMin": 2,
    "estimatedDaysMax": 3
  }
]
```

---

### 5. Webhook Validation Endpoints (Payment Replay Protection)

#### `POST /api/v1/webhooks/paymob`
**Headers:** `X-Paymob-HMAC: <SHA512_HMAC_HEX>`  
**Response Payload (200 OK):**
```json
{
  "success": true,
  "message": "Webhook processed successfully",
  "eventId": "evt_paymob_99120"
}
```

---

### 6. Admin & Health Monitoring Endpoints

#### `GET /api/v1/admin/config`
**Response Payload (200 OK):**
```json
{
  "vendorId": "acme-store",
  "vendorDisplayName": "ACME Store Egypt",
  "tiers": {
    "boot": {
      "auth": { "tokenLifetimeMinutes": 60, "jwtSecret": "ref:env:JWT_SECRET" }
    },
    "runtime": {
      "tax": { "strategy": "Flat", "flatRatePercentage": 14.0 },
      "checkout": { "guestCheckoutEnabled": true, "orderNumberPrefix": "ACM" }
    }
  }
}
```

#### `GET /health/ready`
**Response Payload (200 OK):**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "sqlserver": { "status": "Healthy", "duration": "00:00:00.005" },
    "redis": { "status": "Healthy", "duration": "00:00:00.002" }
  }
}
```
