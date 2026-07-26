# Quickstart Validation Guide: Core Domain Layer Aggregates

**Feature**: 002-domain-layer-aggregates
**Branch**: `002-domain-layer-aggregates`

This guide describes how to validate that the Domain layer implementation is correct end-to-end using the project's test suite. It references [data-model.md](../data-model.md) and [contracts/](../contracts/) for structural details.

---

## Prerequisites

| Requirement | Version / Detail |
|-------------|-----------------|
| .NET SDK | 9.0 (latest) |
| `Vendor.Domain` project | Zero external NuGet references (verify with `dotnet list package`) |
| `Vendor.Domain.Tests` project | xUnit 2.x; zero infrastructure dependencies |

### Verify zero external NuGet dependencies in Domain

```powershell
dotnet list src/Vendor.Domain/Vendor.Domain.csproj package
```

**Expected output**: _No packages are installed._ (or only `Microsoft.AspNetCore.App` framework reference — no NuGet packages).

---

## Setup

No database or external services required. All Domain tests are pure in-memory unit tests.

```powershell
# From repo root — build the Domain project
dotnet build src/Vendor.Domain/Vendor.Domain.csproj

# Build the test project
dotnet build tests/Vendor.Domain.Tests/Vendor.Domain.Tests.csproj
```

---

## Validation Scenarios

Run all domain tests:
```powershell
dotnet test tests/Vendor.Domain.Tests/ --logger "console;verbosity=normal"
```

### Scenario 1 — Product Invariant: Activation Guards

**Test class**: `ProductTests`

| Step | Command / Assertion |
|------|---------------------|
| Create product with price = 0 | `new Product(...)` with `Money(0, "USD")` |
| Attempt `Activate()` | Should throw `BusinessRuleViolationException` |
| Create product with valid price, no images | Attempt `Activate()` → throws |
| Add image, call `Activate()` | Returns `Success`; `Status == Active` |
| Assert domain event | `product.DomainEvents` contains exactly 1 `ProductActivatedEvent` |

---

### Scenario 2 — ProductVariant: Duplicate SKU Rejection

**Test class**: `ProductTests`

| Step | Assertion |
|------|-----------|
| Add variant with SKU "V001" | Accepted |
| Add second variant with same SKU "V001" | Throws `BusinessRuleViolationException` |
| Add variant with SKU "V002" | Accepted |

---

### Scenario 3 — Stock Deduction and Low-Stock Event

**Test class**: `ProductTests`

| Step | Assertion |
|------|-----------|
| Variant stock = 5, threshold = 3 | Initial state |
| `DeductStock(3, threshold: 3)` | Stock = 2; `ProductLowStockEvent` raised |
| `DeductStock(3, threshold: 3)` on stock of 2 | Throws `BusinessRuleViolationException` (negative stock) |

---

### Scenario 4 — Cart: Max Items and Discount Code

**Test class**: `CartTests`

| Step | Assertion |
|------|-----------|
| Add items up to `maxItems = 5` | Accepted |
| Add 6th item | Throws `BusinessRuleViolationException` |
| `ApplyDiscount("SAVE10")` | `Cart.DiscountCode == "SAVE10"` |
| `ApplyDiscount("SUMMER")` | Replaces; `Cart.DiscountCode == "SUMMER"` |
| `RemoveDiscount()` | `Cart.DiscountCode == null` |

---

### Scenario 5 — Cart: Guest-to-Customer Merge

**Test class**: `CartTests`

| Step | Assertion |
|------|-----------|
| Create guest cart with 2 items | `Cart.CustomerId == null` |
| Create customer cart with 1 item | `Cart.CustomerId != null` |
| `customerCart.Merge(guestCart)` | Customer cart has 3 items; `guestCart.Status == Merged` |

---

### Scenario 6 — Cart: Abandonment Predicate

**Test class**: `CartTests`

| Step | Assertion |
|------|-----------|
| Set `LastModifiedUtc = utcNow - 2 hours`, timeout = 1 hour | `IsAbandoned(utcNow, 1 hour) == true` |
| `MarkAbandoned(utcNow, 1 hour)` | `Status == Abandoned`; `CartAbandonedEvent` raised |
| `MarkAbandoned` on already-abandoned cart | Throws |

---

### Scenario 7 — Order: State Machine Enforcement

**Test class**: `OrderTests`

| Step | Assertion |
|------|-----------|
| Create order | `Status == Pending`; `OrderPlacedEvent` raised |
| `ConfirmPayment()` | `Status == Confirmed`; `OrderConfirmedEvent` raised |
| Attempt `Deliver()` directly from `Confirmed` | Throws `InvalidStateTransitionException` |
| `StartProcessing()` → `Ship()` → `Deliver()` | Valid; `OrderDeliveredEvent` raised |
| Attempt `ConfirmPayment()` on delivered order | Throws |

---

### Scenario 8 — Order: Financial Invariant

**Test class**: `OrderTests`

| Step | Assertion |
|------|-----------|
| Subtotal = $100, Tax = $10, Shipping = $5, Discount = $0 | `Total = $115` |
| Apply discount > subtotal + tax + shipping | Constructor/factory throws `BusinessRuleViolationException` |

---

### Scenario 9 — Payment: Refund Guard

**Test class**: `PaymentTests`

| Step | Assertion |
|------|-----------|
| Capture $100 | `Status == Captured` |
| `Refund($60)` | `RefundedAmount = $60`; `Status == PartiallyRefunded` |
| `Refund($50)` | Throws `BusinessRuleViolationException` (total $110 > $100) |
| `Refund($40)` | `RefundedAmount = $100`; `Status == Refunded` |

---

### Scenario 10 — Shipment: Tracking Number Guard

**Test class**: `ShipmentTests`

| Step | Assertion |
|------|-----------|
| `Shipment` in `Pending` state | `TrackingNumber == null` |
| Attempt to set tracking number before `CreateLabel()` | Only `CreateLabel()` method may set tracking number; direct setter is `private` |
| `CreateLabel("TRACK-001", estimatedDelivery)` | `Status == LabelCreated`; `TrackingNumber == "TRACK-001"` |

---

### Scenario 11 — Promotion: Max Usage and Auto-Deactivation

**Test class**: `PromotionTests`

| Step | Assertion |
|------|-----------|
| Create promotion with `MaxUsageCount = 3` | `IsActive == true` |
| `Apply(...)` 2 times | `CurrentUsageCount == 2`; no `PromotionExhaustedEvent` |
| `Apply(...)` 3rd time | `CurrentUsageCount == 3`; `IsActive == false`; `PromotionExhaustedEvent` raised |
| `Apply(...)` 4th attempt | Throws `BusinessRuleViolationException` (inactive) |

---

### Scenario 12 — ReturnRequest: Divergent Completion

**Test class**: `ReturnRequestTests`

| Step | Assertion |
|------|-----------|
| Create `ReturnRequest` with 0 items | Throws `BusinessRuleViolationException` |
| Create with 1+ items | `Status == Pending`; `ReturnRequestCreatedEvent` raised |
| `Approve(ResolutionType.Refund)` | `Status == Approved`; `ReturnRequestApprovedEvent` raised |
| `CompleteReturn()` | `Status == Returned`; `ReturnCompletedEvent` raised |
| Create another request → `Approve(Exchange)` → `CompleteExchange()` | `Status == Exchanged`; `ExchangeCompletedEvent` raised |

---

### Scenario 13 — Money: Currency Mismatch Exception

**Test class**: `MoneyTests`

| Step | Assertion |
|------|-----------|
| `Money(100, "USD") + Money(50, "EUR")` | Throws `CurrencyMismatchException` |
| `Money(100, "USD") + Money(50, "USD")` | Returns `Money(150, "USD")` |
| `Money(100, "USD") * 1.1m` | Returns `Money(110, "USD")` |

---

### Scenario 14 — Slug: Pattern Validation

**Test class**: `SlugTests`

| Input | Outcome |
|-------|---------|
| `"valid-slug-123"` | Accepted |
| `"UPPERCASE"` | Throws `ArgumentException` |
| `"has space"` | Throws `ArgumentException` |
| `"special@char"` | Throws `ArgumentException` |

---

### Scenario 15 — AnalyticsEvent: Immutability

**Test class**: `AnalyticsEventTests`

| Step | Assertion |
|------|-----------|
| `AnalyticsEvent.Capture(customerId, "ProductViewed", payload, consentAtCapture: true)` | `ConsentGrantedAtCapture == true`; `OccurredAtUtc` set; no mutable public setters |
| Attempt to modify any field | No public setters on aggregate; compile-time guard |

---

## Coverage Gate

After running tests, verify Domain coverage meets ≥ 90%:

```powershell
dotnet test tests/Vendor.Domain.Tests/ \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

# Install report tool if not already installed:
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:TextSummary

Get-Content coverage/report/Summary.txt
```

**Expected**: `Line coverage: ≥ 90.0%` for `Vendor.Domain` assembly.

---

## Next Steps

After all 15 validation scenarios pass and coverage ≥ 90%:

```bash
/speckit-tasks
```
