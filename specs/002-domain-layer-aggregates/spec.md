# Feature Specification: Core Domain Layer Aggregates

**Feature Branch**: `002-domain-layer-aggregates`

**Created**: 2026-07-25

**Status**: Draft

**Input**: User description: "Build the Domain layer: Product, ProductVariant, Customer, Cart, Order, Payment, Shipment, Promotion, ReturnRequest, VendorSettings, and AnalyticsEvent aggregates, each with a strongly-typed ID and zero dependency on anything outside the Domain project. Key invariants: Product cannot activate with price <= 0 or no images; variant SKUs must be unique within a product; stock cannot go negative and raises a low-stock event below a configurable threshold. Customer supports one-way guest-to-registered conversion with unique email and tracked analytics consent. Cart enforces a max item count, supports applying/removing a single discount code, detects abandonment via timeout, and merges a guest cart into a customer cart on login. Order enforces a strict state machine Pending -> Confirmed -> Processing -> Shipped -> Delivered, with side branches to Cancelled, RefundRequested -> Refunded, and ReturnRequested/ExchangeRequested -> Returned/Exchanged and its total must always equal subtotal + tax + shipping - discount, never negative; order lines are immutable after creation. Payment refunds can never exceed the captured amount, support partial refunds, and are protected from double-charging by an idempotency key. Shipment status progresses linearly and a tracking number is only set once a label is created. Promotion tracks usage count, auto-deactivates at max usage, caps percentage discounts by a maximum amount, and enforces a validity date range. ReturnRequest must include at least one item and diverges at completion into either a refund (return) or a replacement order (exchange). VendorSettings is the DB-backed runtime config and raises an event on every change. AnalyticsEvent is immutable once captured and stores a snapshot of consent at capture time. Include value objects Money (currency-safe arithmetic, no cross-currency math), Address (owned/embedded, not a separate table), DateRange, Slug (lowercase-alphanumeric-hyphen only), Weight, and Dimensions. Include the 17+ domain events listed in the event table, and repository/adapter interfaces (IProductRepository, ICustomerRepository, ICartRepository, IOrderRepository, IPaymentRepository, IShipmentRepository, IPromotionRepository, IReturnRequestRepository, IVendorSettingsRepository, IAnalyticsEventRepository, IPaymentGateway, IShippingProvider, ITaxCalculator, IAnalyticsForwarder, INotificationSender, ISecretResolver) interfaces only, no implementations."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Product & Catalog Management with Inventory Invariants (Priority: P1)

As a Store Manager, I want to manage products, variants, and stock levels so that invalid products (zero price or no images) cannot be published and inventory levels are accurately tracked with automatic low-stock notifications.

**Why this priority**: Products are the primary entity in an e-commerce catalog; orders and carts cannot exist without valid product domain models.

**Independent Test**: Can be tested by creating products, attaching variants and images, attempting activation under invalid conditions (zero price or missing images), and deducting stock to verify invariant enforcement and low-stock event generation.

**Acceptance Scenarios**:

1. **Given** a product with a valid price (> 0) and at least one image, **When** the manager activates the product, **Then** the product status transitions to Active and a `ProductActivatedEvent` is raised.
2. **Given** a product with price <= 0 or zero images, **When** activation is attempted, **Then** the domain rejects activation and throws a business rule exception.
3. **Given** a product with multiple variants, **When** a new variant is added with a duplicate SKU within the same product, **Then** the domain rejects the variant creation.
4. **Given** a product variant with stock level 5 and low-stock threshold 3, **When** 3 units are deducted, **Then** stock becomes 2 and a `ProductLowStockEvent` is raised.

---

### User Story 2 - Cart Management & Guest-to-Customer Merge (Priority: P1)

As a Shopper (Guest or Registered Customer), I want to manage my shopping cart, apply discount codes, and seamlessly merge my guest cart into my account upon login.

**Why this priority**: Essential checkout preparation step; directly impacts conversion and user shopping experience.

**Independent Test**: Can be tested by creating guest carts, adding items up to max capacity, applying/removing discount codes, and calling the merge operation with a customer account.

**Acceptance Scenarios**:

1. **Given** an active shopping cart, **When** a shopper adds items exceeding `maxItemsPerOrder`, **Then** the domain rejects the addition.
2. **Given** a cart with items, **When** a shopper applies a valid coupon code, **Then** the discount code is attached; applying a second coupon replaces the previous one.
3. **Given** an anonymous guest cart containing items, **When** the guest logs in as a registered customer with an existing cart, **Then** the guest cart items are merged into the customer cart and the guest cart is marked merged/cleared.

---

### User Story 3 - Order Lifecycle State Machine & Financial Invariants (Priority: P1) 🎯 MVP

**Goal**: Enforce strict lifecycle transitions (`Pending` -> `Confirmed` -> `Processing` -> `Shipped` -> `Delivered`, with `Cancelled`, `RefundRequested`/`Refunded`, `ReturnRequested`/`ExchangeRequested` side paths) and financial balance invariant (`Total = Subtotal + Tax + Shipping - Discount >= 0`).

**Why this priority**: Core transaction model of the platform. Accurate financial math and state machine enforcement prevent illegal order states and financial loss.

**Independent Test**: Can be tested by constructing orders, applying valid vs invalid state transitions, and verifying total calculation math across various price/tax/discount combinations.

**Acceptance Scenarios**:

1. **Given** a newly created order in `Pending` state, **When** payment is confirmed, **Then** the state transitions to `Confirmed` and `OrderConfirmedEvent` is raised; attempting to transition directly to `Delivered` fails.
2. **Given** an order line item, **When** the order is created, **Then** order lines become completely immutable (cannot change quantity or price).
3. **Given** an order calculation, **When** total is calculated, **Then** `Total` equals `Subtotal + Tax + Shipping - Discount` and cannot be negative.

---

### User Story 4 - Payment Capture, Refund Protection & Shipment Progression (Priority: P2)

As a Finance & Fulfillment Officer, I want payment operations to prevent double-charging and over-refunding, and shipment tracking to enforce linear progression.

**Why this priority**: Protects against double payments, invalid refunds, and inaccurate shipping status updates.

**Independent Test**: Can be tested by initiating payments with idempotency keys, executing partial/full refunds against captured amounts, and progressing shipment states.

**Acceptance Scenarios**:

1. **Given** a captured payment of $100, **When** a refund of $60 is processed followed by a second refund of $50, **Then** the second refund fails because cumulative refunds ($110) exceed captured amount ($100).
2. **Given** a payment request with an `IdempotencyKey`, **When** duplicate payment requests with the same key are submitted, **Then** the domain returns the existing payment record without re-charging.
3. **Given** a shipment in `Pending` state, **When** a tracking number is assigned before label creation, **Then** the operation is rejected; tracking numbers can only be set upon label creation (`LabelCreated` state).

---

### User Story 5 - Promotions, Returns/Exchanges, VendorSettings & Analytics (Priority: P3)

As an Administrator or Customer, I want promotion validity/caps enforced, return vs exchange flows separated, runtime settings tracked, and immutable consent-aware analytics captured.

**Why this priority**: Full suite of post-purchase, marketing, and governance operations.

**Independent Test**: Can be tested by evaluating promotion max usage/caps, completing return vs exchange requests, updating vendor settings, and recording analytics events.

**Acceptance Scenarios**:

1. **Given** a promotion with `maxUsageCount = 100`, **When** the 100th usage is recorded, **Then** the promotion auto-deactivates and raises `PromotionExhaustedEvent`.
2. **Given** an approved `ReturnRequest`, **When** completed as a Return, **Then** a refund is issued; when completed as an Exchange, a replacement order is created.
3. **Given** an `AnalyticsEvent`, **When** captured, **Then** it records a snapshot of customer consent at capture time and becomes completely immutable.

---

### Edge Cases

- What happens if currency mismatch occurs in `Money` arithmetic? `Money` constructor/methods throw `CurrencyMismatchException` when adding or subtracting different currencies.
- What happens if a `Slug` contains uppercase letters or spaces? Construction throws `ArgumentException`; only `^[a-z0-9\-]+$` is allowed.
- How does Customer conversion handle existing guest consent? When a guest converts to a registered customer, consent history is preserved and appended with a registration event.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Domain layer MUST have **zero external NuGet package references** (BCL net9.0 only).
- **FR-002**: System MUST define strongly-typed IDs for all 11 aggregate roots:
  `ProductId`, `ProductVariantId`, `CustomerId`, `CartId`, `OrderId`, `PaymentId`, `ShipmentId`, `PromotionId`, `ReturnRequestId`, `VendorSettingsId`, `AnalyticsEventId`.
- **FR-003**: System MUST encapsulate aggregate logic and invariants:
  - **Product**: Cannot activate with price <= 0 or zero images. Variant SKUs must be unique per product. Stock cannot go negative; raises `ProductLowStockEvent` when below threshold.
  - **Customer**: One-way guest-to-registered conversion. Email uniqueness. Analytics consent tracking.
  - **Cart**: Max item limit. Single discount code toggle. Abandonment timeout detection. Guest-to-customer cart merge on login.
  - **Order**: Strict state machine (`Pending` -> `Confirmed` -> `Processing` -> `Shipped` -> `Delivered`; side branches: `Cancelled`, `RefundRequested` -> `Refunded`, `ReturnRequested`/`ExchangeRequested` -> `Returned`/`Exchanged`). Immutable order lines. Total formula: `Subtotal + Tax + Shipping - Discount >= 0`.
  - **Payment**: Refunds <= captured amount. Partial refund support. Idempotency key protection.
  - **Shipment**: Linear status progression. Tracking number set only on label creation.
  - **Promotion**: Usage count tracking, auto-deactivation at max usage, percentage discount cap, validity date range.
  - **ReturnRequest**: Minimum 1 item. Divergence on completion: Return -> refund, Exchange -> replacement order.
  - **VendorSettings**: DB-backed runtime config. Raises `VendorSettingsUpdatedEvent` on every change.
  - **AnalyticsEvent**: Immutable snapshot of event data + consent status at capture time.
- **FR-004**: System MUST define Value Objects with currency-safe arithmetic and domain validation:
  - `Money` (Amount, CurrencyCode; no cross-currency math)
  - `Address` (Street, City, State, ZipCode, CountryCode; owned/embedded)
  - `DateRange` (StartUtc, EndUtc; End >= Start)
  - `Slug` (Value; pattern `^[a-z0-9\-]+$`)
  - `Weight` (Value, Unit)
  - `Dimensions` (Length, Width, Height, Unit)
- **FR-005**: System MUST define 22 domain events across all aggregate lifecycles.
- **FR-006**: System MUST define repository interfaces in Domain:
  `IProductRepository`, `ICustomerRepository`, `ICartRepository`, `IOrderRepository`, `IPaymentRepository`, `IShipmentRepository`, `IPromotionRepository`, `IReturnRequestRepository`, `IVendorSettingsRepository`, `IAnalyticsEventRepository`.
- **FR-007**: System MUST define adapter interfaces in Domain:
  `IPaymentGateway`, `IShippingProvider`, `ITaxCalculator`, `IAnalyticsForwarder`, `INotificationSender`, `ISecretResolver`.

### Key Entities

- **Product**: Aggregate root managing product details, variants, images, and inventory events.
- **ProductVariant**: Entity representing SKU, price adjustment, and stock count within a product.
- **Customer**: Aggregate root representing shoppers (guest or registered) and consent state.
- **Cart**: Aggregate root representing transient shopping items, coupon codes, and merge state.
- **Order**: Aggregate root representing placed orders, state machine transitions, and financial totals.
- **Payment**: Aggregate root representing transaction charges, refunds, and idempotency tokens.
- **Shipment**: Aggregate root representing package fulfillment, tracking, and carrier status.
- **Promotion**: Aggregate root representing discount rules, caps, and usage limits.
- **ReturnRequest**: Aggregate root representing return/exchange claims and fulfillment resolution.
- **VendorSettings**: Aggregate root representing runtime configuration state.
- **AnalyticsEvent**: Aggregate root representing captured event telemetry and consent snapshot.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Domain project classes have zero external NuGet package dependencies.
- **SC-002**: 100% of aggregate invariant violations (invalid state transition, negative stock, currency mismatch, raw secrets) throw explicit, testable domain exceptions.
- **SC-003**: Domain unit test coverage reaches or exceeds 90% line coverage.
- **SC-004**: 100% of financial math operations (`Money`) prevent cross-currency operations at compile/runtime.

## Assumptions

- Strongly-typed IDs are implemented as readonly record structs or C# 12 primary constructor structs wrapping `Guid` or `string`.
- All domain events implement a marker interface `IDomainEvent` containing `OccurredOnUtc`.
