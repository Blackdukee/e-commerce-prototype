# Research: Infrastructure Layer & Persistence

**Feature**: 004-infrastructure-layer-persistence  
**Date**: 2026-07-25  

---

## R1: EF Core 9 Owned Types, JSON Columns & Soft Delete Query Filters

**Decision**: Configure EF Core 9 entity mappings using `IEntityTypeConfiguration<T>` implementations in `Vendor.Infrastructure.Persistence.Configurations`:
- `Money` and `Address` are mapped as owned types via `builder.OwnsOne(x => x.Money, m => { m.Property(p => p.Amount).HasColumnName("PriceAmount"); m.Property(p => p.Currency).HasColumnName("PriceCurrency").HasMaxLength(3); })`.
- Primitive collections (`Images`, `Tags`, `Categories`, `Attributes`) are stored as JSON via `.PrimitiveCollection(x => x.Images)` / EF Core 9 `.ToJson()` or JSON value converters (`HasConversion(...)`).
- Soft delete filters are registered globally using `builder.HasQueryFilter(x => !x.IsDeleted)` for `Product` and `Customer`.
- SQL Server transient retry resilience enabled via `options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null))`.

**Rationale**: `OwnsOne` embeds value objects directly into the parent table without creating additional relational foreign key tables, directly satisfying Constitution Principle III. EF Core 9's native JSON column support eliminates custom serialization boilerplate for array/dictionary fields.

---

## R2: Transactional Outbox Pattern with EF Core Interceptor & Background Worker

**Decision**: Implement an EF Core `SaveChangesInterceptor` (`OutboxInterceptor`) that intercepts `SavingChangesAsync` before database commit:
1. Scans tracked `DbContext` entities inheriting `AggregateRoot<TId>`.
2. Extracts raised `IDomainEvent` instances from each aggregate root via `GetDomainEvents()`.
3. Serializes events into `OutboxMessage` entity rows (`Id`, `Type`, `Content`, `OccurredOnUtc`).
4. Clears domain events from aggregate roots.
5. Adds `OutboxMessage` entities to `VendorDbContext` so they are persisted in the **exact same database transaction** as aggregate mutations.

Background dispatching is handled by `OutboxProcessorHostedService` (`IHostedService`):
- Polls `OutboxMessages` every 2 seconds.
- Fetches up to 20 unprocessed messages ordered by `OccurredOnUtc`.
- Publishes each event via MediatR `IPublisher.Publish(domainEvent, ct)`.
- Updates `ProcessedOnUtc = DateTime.UtcNow` on success.
- Increments `RetryCount` on failure; after 3 retries, sets `Error` and moves record to a dead-letter state.

**Rationale**: Outbox pattern guarantees at-least-once domain event delivery without requiring two-phase distributed transactions across SQL database and message brokers.

---

## R3: Multi-Provider Payment Gateway Webhook Signature Verification

**Decision**: Cryptographically validate incoming webhooks in each gateway adapter before processing payload:
- **Stripe**: Invoke `Stripe.EventUtility.ConstructEvent(json, stripeSignatureHeader, webhookSecret, tolerance: 300)` using HMAC SHA-256.
- **PayPal**: Post payload to PayPal REST API `/v1/notifications/verify-webhook-signature` containing `auth_algo`, `cert_url`, `transmission_id`, `transmission_sig`, `transmission_time`, `webhook_id`, and raw request body.
- **Paymob**: Extract payload parameters, lexicographically sort by parameter key, concatenate values without separators, and compute HMAC SHA-512 using the vendor's HMAC secret key. Compare calculated hash against `hmac` payload field.

**Rationale**: Webhook verification prevents spoofed payment notification attacks and guarantees that order confirmation / refund state transitions are triggered exclusively by verified payment processor signatures.

---

## R4: Dual-Mode Caching & SignalR Redis Backplane

**Decision**: Register caching services based on vendor configuration `Caching:Provider`:
- If `Provider == "Memory"`: register `InMemoryCacheService` wrapping `IMemoryCache`.
- If `Provider == "Redis"`: register `RedisCacheService` wrapping `IDistributedCache` (`StackExchange.Redis`). Auto-configure SignalR backplane via `services.AddSignalR().AddStackExchangeRedis(redisConnectionString)`.

Domain event handlers (`ProductUpdatedEventHandler`, `PromotionUpdatedEventHandler`) subscribe to aggregate mutation events and call `ICacheService.RemoveAsync(...)` on invalidation keys.

**Rationale**: Single configuration key toggle allows local single-instance deployments to run with zero external infrastructure while scaling horizontally across multi-instance cloud clusters with a single config change.
