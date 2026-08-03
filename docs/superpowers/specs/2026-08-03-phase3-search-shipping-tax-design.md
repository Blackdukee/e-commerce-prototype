# Phase 3 — Search Engine & Live Shipping/Tax APIs

**Date:** 2026-08-03  
**Status:** Approved  
**Scope:** Vendor e-commerce platform  

---

## 1. Goals

Replace hardcoded stubs for product search, shipping rates, and tax calculation with
real production-ready adapters, each following the codebase's established **hybrid pattern**:
live external API when configured → safe fallback in local development.

---

## 2. Architecture Principles

- **No breaking interface changes.** All three integrations sit behind existing domain
  adapter interfaces (`IShippingProvider`, `ITaxCalculator`) or a new Application interface
  (`IProductSearchService`).
- **Config-gated activation.** External clients activate only when their API key / URL is
  present in configuration. Missing config → fallback to existing stubs.
- **Fail-safe.** HTTP failures in live adapters catch exceptions and fall back rather than
  propagating errors to the checkout flow.
- **Testable in isolation.** Every live adapter is tested with mocked `HttpMessageHandler`
  or mocked `ElasticsearchClient`; no real network required.

---

## 3. Feature: Product Search (`IProductSearchService`)

### 3.1 Interface

Location: `src/Vendor.Application/Common/Interfaces/IProductSearchService.cs`

```csharp
public interface IProductSearchService
{
    Task<PagedResult<ProductSearchDoc>> SearchProductsAsync(
        string? query,
        ProductSearchFilters filters,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task IndexProductAsync(ProductSearchDoc doc, CancellationToken ct = default);
    Task DeleteProductIndexAsync(string productId, CancellationToken ct = default);
}
```

### 3.2 Search Document

Location: `src/Vendor.Application/Common/Models/ProductSearchDoc.cs`

Fields: `Id` (string), `Name`, `Slug`, `Description`, `BasePrice` (decimal),
`Currency`, `Status` (string), `CreatedAtUtc`.

### 3.3 Search Filters

Location: `src/Vendor.Application/Common/Models/ProductSearchFilters.cs`

Fields: `MinPrice` (decimal?), `MaxPrice` (decimal?), `Status` (string? — defaults to "Active").

### 3.4 Elasticsearch Adapter

Location: `src/Vendor.Infrastructure/Search/ElasticsearchProductSearchService.cs`

- NuGet: `Elastic.Clients.Elasticsearch` (8.x).
- Index name: `products` (configurable via `Elasticsearch:IndexName`, default `products`).
- `SearchProductsAsync`: multi-match query on `name`, `description`; term filter on `status`;
  range filter on `base_price`; paginated via `from`/`size`.
- `IndexProductAsync`: `IndexAsync` with document ID = product ID.
- `DeleteProductIndexAsync`: `DeleteAsync` by document ID.
- Constructor takes `ElasticsearchClient` (injected).

### 3.5 EF Core Fallback Adapter

Location: `src/Vendor.Infrastructure/Search/EfCoreProductSearchService.cs`

- Takes `VendorDbContext` (scoped — service is scoped).
- `SearchProductsAsync`: `IQueryable<Product>` with `EF.Functions.Like` on `Name` and
  `Description`; price range filter; status filter; `Skip`/`Take` pagination.
- `IndexProductAsync` / `DeleteProductIndexAsync`: no-ops (EF reads from primary DB).

### 3.6 Hybrid Selector

Location: `src/Vendor.Infrastructure/Search/HybridProductSearchService.cs`

- Constructor takes `ElasticsearchProductSearchService` and `EfCoreProductSearchService`.
- Selects Elasticsearch when `Elasticsearch:Uri` is configured and non-empty; otherwise EF Core.
- Registered as `IProductSearchService` (scoped — follows EF Core lifetime).

### 3.7 Index Sync

**Background sync** — `ProductIndexSyncJob` (Hangfire):
- Location: `src/Vendor.Infrastructure/Search/ProductIndexSyncJob.cs`
- Recurring schedule: every 5 minutes.
- Fetches all `Active` products from DB and calls `IndexProductAsync` for each.
- Registered in DI and scheduled in `DependencyInjection.cs`.

**Event-driven sync** — on `ProductActivatedEvent` raised via outbox:
- `ProductIndexedEventHandler` in Application layer calls `IProductSearchService.IndexProductAsync`
  immediately when a product is activated.

### 3.8 Search Endpoint

`GET /api/v1/products/search`

Query parameters: `q` (string?), `minPrice` (decimal?), `maxPrice` (decimal?),
`status` (string?, default `Active`), `page` (int, default 1), `pageSize` (int, default 20, max 100).

- Public — no authentication required.
- Returns `PagedResult<ProductSearchDoc>`.
- Location: `src/Vendor.Api/Endpoints/ProductSearchEndpoints.cs`, registered via `MapProductSearchEndpoints`.

---

## 4. Feature: Live Shipping Rates (Shippo)

### 4.1 Existing Interface

`IShippingProvider` in `Vendor.Domain.Interfaces.Adapters` — **no changes**.

### 4.2 Live Shippo Adapter (Replace Stub)

Location: `src/Vendor.Infrastructure/Shipping/ShippoShippingProvider.cs` (replace stub body)

- Uses `IHttpClientFactory` typed client: `ShippoHttpClient`.
- Base URL: `https://api.goshippo.com/`.
- Authentication: `ShippoToken {apiKey}` Authorization header.
- `GetRatesAsync`: POST `/shipments` with parcel and address data; parse `rates` array
  into `IReadOnlyList<ShippingRate>`.
- `CreateLabelAsync`: POST `/transactions` with selected rate token; parse tracking number
  and label URL.
- `TrackShipmentAsync`: GET `/tracks/{carrier}/{trackingNumber}`; parse status and
  estimated delivery.
- All responses deserialized via `System.Text.Json`.

### 4.3 Hybrid Shipping Provider

Location: `src/Vendor.Infrastructure/Shipping/HybridShippingProvider.cs`

- Wraps `ShippoShippingProvider` and `FlatRateShippingProvider`.
- When `Shippo:ApiKey` is configured → use Shippo; on `HttpRequestException` → log warning
  and fall back to flat rate.
- When `Shippo:ApiKey` is not configured → use flat rate directly.
- Registered as `IShippingProvider` scoped.

### 4.4 Rates Endpoint

`GET /api/v1/shipments/rates`

Query parameters: `originZip`, `destinationZip`, `weightGrams` (int), `currency` (default `USD`).

- Requires authentication.
- Returns `IReadOnlyList<ShippingRateDto>` (carrier, service, amount, currency, estimatedDays).
- Location: `src/Vendor.Api/Endpoints/ShipmentRatesEndpoints.cs`.

---

## 5. Feature: Dynamic Tax Calculation (TaxJar)

### 5.1 Existing Interface

`ITaxCalculator` in `Vendor.Domain.Interfaces.Adapters` — **no changes**.

### 5.2 Live TaxJar Adapter

Location: `src/Vendor.Infrastructure/Tax/TaxJarTaxCalculator.cs`

- Uses `IHttpClientFactory` typed client: `TaxJarHttpClient`.
- Base URL: `https://api.taxjar.com/v2/`.
- Authentication: `Bearer {apiKey}` Authorization header.
- `CalculateTaxAsync`: POST `/taxes` with `from_zip`, `to_zip`, `amount` (subtotal),
  `shipping` (sum of shipping costs), `line_items` array.
- Parses `tax.amount_to_collect` → `Money(amount, currencyCode)`.
- On `HttpRequestException` → falls back to `FlatTaxCalculator.CalculateTaxAsync`.

### 5.3 Hybrid Tax Calculator

Location: `src/Vendor.Infrastructure/Tax/HybridTaxCalculator.cs`

- Wraps `TaxJarTaxCalculator` and `FlatTaxCalculator`.
- When `TaxJar:ApiKey` configured → use TaxJar; on failure → fallback.
- When `TaxJar:ApiKey` not configured → flat rate directly.
- Registered as `ITaxCalculator` scoped.

---

## 6. Configuration

| Key | Purpose | Required for |
|-----|---------|-------------|
| `Elasticsearch:Uri` | Elasticsearch cluster URI | Search |
| `Elasticsearch:IndexName` | Index name (default: `products`) | Search |
| `Shippo:ApiKey` | Shippo API token | Live shipping |
| `TaxJar:ApiKey` | TaxJar API key | Live tax |

All keys are optional — missing = fallback mode.

---

## 7. DI Registration Changes

In `src/Vendor.Infrastructure/DependencyInjection.cs`:

- Register `ElasticsearchClient` singleton when `Elasticsearch:Uri` is set.
- Register `ElasticsearchProductSearchService` and `EfCoreProductSearchService` scoped.
- Register `HybridProductSearchService` as `IProductSearchService` scoped.
- Register `ShippoHttpClient` via `services.AddHttpClient<ShippoShippingProvider>()`.
- Register `TaxJarHttpClient` via `services.AddHttpClient<TaxJarTaxCalculator>()`.
- Register `HybridShippingProvider` as `IShippingProvider` scoped.
- Register `HybridTaxCalculator` as `ITaxCalculator` scoped.
- Schedule `ProductIndexSyncJob` recurring via Hangfire every 5 min.

In `src/Vendor.Api/Extensions/WebApplicationExtensions.cs`:
- Add `v1.MapProductSearchEndpoints()`.
- Add `v1.MapShipmentRatesEndpoints()`.

---

## 8. Testing

| Test File | Type | Covers |
|-----------|------|--------|
| `ElasticsearchProductSearchServiceTests.cs` | Unit | Index, search, delete with mocked client |
| `HybridProductSearchServiceTests.cs` | Unit | Selects ES when URI set; EF Core when not |
| `EfCoreProductSearchServiceTests.cs` | Unit | LIKE queries, pagination, filters |
| `ShippoShippingProviderTests.cs` | Unit | HTTP success path, response mapping |
| `HybridShippingProviderTests.cs` | Unit | Config-gated selection and exception fallback |
| `TaxJarTaxCalculatorTests.cs` | Unit | HTTP success path, response mapping |
| `HybridTaxCalculatorTests.cs` | Unit | Config-gated selection and exception fallback |
| `ProductSearchEndpointTests.cs` | Integration | GET /search with EF Core fallback active |
| `ShipmentRatesEndpointTests.cs` | Integration | GET /rates requires auth, returns rates |

---

## 9. Out of Scope

- Elasticsearch index mapping migrations / schema versioning.
- Multi-language / synonym search.
- TaxJar address validation.
- Shippo webhook processing (covered in Phase 2 via generic webhook handler).
- Real Shippo / TaxJar sandbox calls in CI (no external network in tests).
