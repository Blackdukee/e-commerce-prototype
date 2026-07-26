# Feature Specification: Application Layer CQRS & Pipeline Architecture

**Feature Branch**: `003-application-layer-cqrs`

**Created**: 2026-07-25

**Status**: Draft

**Input**: User description: "Build the Application layer on top of the Domain aggregates: ~35 commands and ~15 queries across Auth, Products, Customers, Cart, Orders, Payments, Shipments, Promotions, Returns, Analytics, and VendorSettings modules (see the command/query table for the full list per module). Every handler returns a Result<T> and never throws for business-logic failures: Success maps to 200/201, a "not found" failure maps to 404, a validation failure maps to 422 with field-level errors, and any other failure maps to 400 with an error code the API layer can branch on. Requests flow through a five-stage pipeline in this order: logging (records request name, user context, duration), validation (runs business rules, short-circuits to a validation-failure result), idempotency (only for requests marked idempotent returns the cached result for a duplicate key instead of re-running the handler), transaction (commands only wraps the handler in a DB transaction with rollback on failure), and performance (warns when a request exceeds 500ms). Two orchestration flows need explicit handling: (1) Checkout validate the cart isn't empty, verify stock for every line, evaluate an optional discount code, calculate tax, open a transaction, create the Order and Payment, decrement stock, record promotion usage, clear the cart, commit, then initiate payment with the gateway. (2) Return/Exchange customer submits a return or exchange request (Pending), admin approves it, customer ships items back, admin marks items received, then admin completes it a return issues a refund and restocks items, an exchange creates a replacement order and restocks the originals. Also define the cross-cutting application interfaces: IUnitOfWork, IIdempotencyStore, ICacheService, ICurrentUserService, ITokenService, IExternalAuthService, IDateTimeProvider."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Deterministic Command/Query Execution with 5-Stage Pipeline (Priority: P1) 🎯 MVP

As an Application Developer and API Consumer, I want all commands and queries to execute through a standardized 5-stage pipeline (Logging → Validation → Idempotency → Transaction → Performance) and return a `Result<T>` wrapper so that failures never throw uncaught business exceptions and map deterministically to HTTP status codes (200/201, 400, 404, 422).

**Why this priority**: The pipeline and `Result<T>` error handling contract are the foundational architecture for all ~50 application handlers.

**Independent Test**: Execute commands/queries with valid inputs, invalid fields, non-existent entity IDs, duplicate idempotency keys, and simulated slow execution to verify pipeline ordering, transaction rollback, and status code mapping.

**Acceptance Scenarios**:

1. **Given** a valid command request, **When** handled, **Then** it executes through Logging, Validation, Transaction, and Performance stages, commits the transaction, and returns `Result.Success(data)` (mapping to 200/201).
2. **Given** a command with invalid inputs (e.g., negative price or blank email), **When** handled, **Then** the Validation stage short-circuits before the Transaction stage, returning `Result.Failure(ValidationError)` containing field-level error messages (mapping to 422).
3. **Given** a request for a non-existent entity ID, **When** handled, **Then** the handler returns `Result.Failure(NotFoundError)` (mapping to 404).
4. **Given** an idempotent command submitted with a duplicate idempotency key, **When** handled, **Then** the Idempotency stage short-circuits execution and returns the cached result without re-executing the handler or reopening a transaction.
5. **Given** a command handler that encounters a business rule violation or DB exception, **When** handled, **Then** the Transaction stage rolls back the database transaction and returns `Result.Failure(Error)` with a specific error code (mapping to 400).
6. **Given** a request taking >500ms, **When** handled, **Then** the Performance stage logs a warning signal while returning the handler result.

---

### User Story 2 - Complete E-Commerce Checkout Orchestration Flow (Priority: P1)

As a Shopper, I want to complete checkout from my active cart so that stock is verified/decremented, taxes and discounts are calculated, an order and payment are created atomically, the cart is cleared, and payment gateway authorization is initiated.

**Why this priority**: Core revenue-generating transaction flow connecting Cart, Product stock, Promotion, Tax, Order, Payment, and Payment Gateway.

**Independent Test**: Execute the `CheckoutOrderCommand` against an active cart containing items, verify stock availability check, discount evaluation, tax calculation, atomic database commit (Order + Payment + Stock Deduction + Promotion Usage + Cart Clear), and payment gateway initiation.

**Acceptance Scenarios**:

1. **Given** an active cart with items, **When** checkout is executed, **Then** the system validates the cart is non-empty, checks stock for all variant lines, calculates tax via `ITaxCalculator`, evaluates any discount code via `Promotion`, opens a transaction, creates `Order` and `Payment`, decrements stock, records promotion usage, clears the cart, commits the transaction, and initiates payment with `IPaymentGateway`.
2. **Given** a cart containing an item with insufficient stock, **When** checkout is executed, **Then** the checkout fails with a stock availability error, the transaction rolls back, and no order or payment is created.
3. **Given** a payment gateway failure after the local database transaction commits, **When** checkout is executed, **Then** the payment record is marked `Failed`, the order transitions to `PendingPayment` or `Cancelled`, and a failure result is returned.

---

### User Story 3 - Multi-Stage Return and Exchange Lifecycle Flow (Priority: P2)

As a Customer and Store Administrator, I want to process returns and exchanges through a multi-stage lifecycle (Submit → Approve/Reject → Mark Received → Complete) so that returns issue refunds and restock items, while exchanges create replacement orders and restock original items.

**Why this priority**: Essential post-purchase customer support and inventory reconciliation flow.

**Independent Test**: Execute return/exchange request submission, administrative approval, receipt confirmation, and final completion for both return (refund) and exchange (replacement order) paths.

**Acceptance Scenarios**:

1. **Given** a delivered order, **When** a customer submits a return request, **Then** a `ReturnRequest` aggregate is created in `Pending` status.
2. **Given** a `Pending` return request, **When** an admin approves it specifying `Refund` or `Exchange` resolution, **Then** the request transitions to `Approved`.
3. **Given** an `Approved` return request, **When** customer ships items back and admin marks items received, **Then** the request transitions to `ItemsReceived`.
4. **Given** a return request marked `ItemsReceived` with `Refund` resolution, **When** completed, **Then** the payment is refunded via `IPaymentGateway`, returned variant stock is restored, and status transitions to `Returned`.
5. **Given** a return request marked `ItemsReceived` with `Exchange` resolution, **When** completed, **Then** a new replacement `Order` is created, original variant stock is restored, and status transitions to `Exchanged`.

---

### User Story 4 - Modular CQRS Application Services (Priority: P2)

As an API Layer, I want access to ~35 commands and ~15 queries organized into 11 domain modules (Auth, Products, Customers, Cart, Orders, Payments, Shipments, Promotions, Returns, Analytics, VendorSettings) so that every administrative and store-front interaction is served by dedicated application handlers.

**Why this priority**: Comprehensive administrative and operational coverage across all platform features.

**Independent Test**: Execute sample commands and queries across each of the 11 modules to verify handler isolation, DTO mapping, and repository/adapter interaction.

**Acceptance Scenarios**:

1. **Given** an Auth command (`RegisterCustomer`, `LoginWithPassword`), **When** executed, **Then** `ITokenService` generates JWT access/refresh tokens and returns authenticated user profiles.
2. **Given** Product catalog commands/queries, **When** executed, **Then** products and variants are created, updated, activated, or searched with cached responses where appropriate.
3. **Given** VendorSettings patch commands, **When** executed, **Then** runtime configuration is updated, cached options are invalidated, and `VendorSettingsUpdatedEvent` is enqueued to outbox.

---

### Edge Cases

- What happens if a database transaction fails during `CheckoutOrderCommand` after stock is decremented? The transaction rolls back completely; stock deductions, order creation, and cart clearing are undone.
- What happens if a duplicate idempotent request arrives while the original request is still processing in Kestrel? The idempotency store detects an in-flight key and returns a 409 Conflict or waits/returns cached result.
- What happens if `ITokenService` or `IExternalAuthService` is unavailable during auth commands? The handler returns `Result.Failure(Error.Custom("AUTH_SERVICE_UNAVAILABLE", ...))` mapping to a 400 Bad Request.

---

## Requirements *(mandatory)*

### Functional Requirements

#### Architecture & Pipeline
- **FR-001**: The Application layer MUST depend **only on `Vendor.Domain`** and abstraction NuGet packages (MediatR, FluentValidation). It MUST NOT reference any infrastructure implementations (EF Core, HTTP clients, SQL SDKs).
- **FR-002**: Every command and query handler MUST return `Result<T>` (or `Result` for void operations) and **NEVER throw exceptions** for business logic or validation failures.
- **FR-003**: System MUST define explicit error variant types mapping to standard HTTP status codes:
  - `Result.Success` → HTTP 200 / 201
  - `Result.Failure(NotFoundError)` → HTTP 404
  - `Result.Failure(ValidationError)` → HTTP 422 (with field-level error dictionary)
  - `Result.Failure(Error)` → HTTP 400 (with machine-readable error code string)
- **FR-004**: All requests MUST flow through a strict **5-stage MediatR pipeline behavior chain** in exact order:
  1. `LoggingBehavior`: Logs request name, masked user context, and execution duration.
  2. `ValidationBehavior`: Executes all registered FluentValidation rules; short-circuits with `ValidationError` (422) if invalid.
  3. `IdempotencyBehavior`: Checks `IIdempotencyStore` for requests implementing `IIdempotentRequest`; returns cached `Result<T>` on duplicate key.
  4. `TransactionBehavior`: Wraps `ICommand` (write operations) in an atomic database transaction via `IUnitOfWork`; rolls back on failure.
  5. `PerformanceBehavior`: Logs a performance warning when execution time exceeds 500ms threshold.

#### Cross-Cutting Application Interfaces
- **FR-005**: System MUST define the following 7 core application interfaces in `Vendor.Application/Interfaces/`:
  - `IUnitOfWork`: Transaction boundary (`BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`).
  - `IIdempotencyStore`: Caching/retrieving idempotent request responses (`GetResultAsync`, `SaveResultAsync`).
  - `ICacheService`: Application caching (`GetAsync`, `SetAsync`, `RemoveAsync`).
  - `ICurrentUserService`: Exposes current authenticated `UserId`, `CustomerId`, `VendorId`, and `Roles`.
  - `ITokenService`: JWT generation, validation, and refresh token handling (`GenerateTokens`, `ValidateToken`).
  - `IExternalAuthService`: OAuth 2.0 provider verification for Google and Facebook (`VerifyGoogleTokenAsync`, `VerifyFacebookTokenAsync`).
  - `IDateTimeProvider`: System time abstraction (`UtcNow`).

#### Module Command & Query Inventory
- **FR-006**: System MUST implement ~35 commands and ~15 queries organized across 11 modules:

| Module | Command / Query | Description |
|--------|-----------------|-------------|
| **Auth** | `RegisterCustomerCommand` | Register customer with password |
| | `LoginWithPasswordCommand` | Authenticate customer via email/password |
| | `LoginWithOAuthCommand` | Authenticate via Google/Facebook OAuth token |
| | `RefreshTokenCommand` | Rotate JWT access and refresh tokens |
| | `RevokeTokenCommand` | Invalidate refresh token |
| | `ChangePasswordCommand` | Update user password |
| | `GetCurrentUserProfileQuery` | Retrieve profile of authenticated user |
| | `ValidateTokenQuery` | Validate JWT token integrity |
| **Products** | `CreateProductCommand` | Create new product in Draft state |
| | `UpdateProductCommand` | Update product details and slug |
| | `ActivateProductCommand` | Activate product (validates price & images) |
| | `DeactivateProductCommand` | Transition product to Draft |
| | `AddProductVariantCommand` | Add variant with unique SKU guard |
| | `UpdateProductVariantCommand` | Update variant price, weight, dimensions |
| | `DeleteProductVariantCommand` | Remove variant (retains >= 1 variant) |
| | `AddProductImageCommand` | Attach image URL to product |
| | `RemoveProductImageCommand` | Detach image URL |
| | `GetProductByIdQuery` | Retrieve product details by ID |
| | `GetProductBySlugQuery` | Retrieve product details by slug |
| | `SearchProductsQuery` | Filter and paginate product catalog |
| | `GetProductVariantsQuery` | List variants for a product |
| **Customers** | `RegisterGuestCustomerCommand` | Create guest customer record |
| | `ConvertGuestToRegisteredCommand` | Convert guest to registered user |
| | `UpdateCustomerProfileCommand` | Update name and contact info |
| | `AddShippingAddressCommand` | Add shipping address to profile |
| | `RemoveShippingAddressCommand` | Remove shipping address |
| | `UpdateAnalyticsConsentCommand` | Grant or revoke analytics consent |
| | `GetCustomerByIdQuery` | Retrieve customer profile by ID |
| | `GetCustomerByEmailQuery` | Retrieve customer profile by email |
| | `GetCustomerOrderHistoryQuery` | List orders for customer |
| **Cart** | `CreateCartCommand` | Initialize guest or customer cart |
| | `AddCartItemCommand` | Add item to cart (enforces max items) |
| | `UpdateCartItemQuantityCommand` | Update item quantity |
| | `RemoveCartItemCommand` | Remove item from cart |
| | `ApplyCartDiscountCodeCommand` | Apply/replace single promotion code |
| | `RemoveCartDiscountCodeCommand` | Clear discount code |
| | `ClearCartCommand` | Remove all items from cart |
| | `MergeGuestCartCommand` | Merge guest cart items into customer cart |
| | `ProcessCartAbandonmentCommand` | Evaluate and mark abandoned carts |
| | `GetCartByIdQuery` | Retrieve cart by ID |
| | `GetCartByCustomerIdQuery` | Retrieve active cart for customer |
| | `GetCartBySessionIdQuery` | Retrieve guest cart by session |
| **Orders** | `PlaceOrderCommand` | Create order from line items |
| | `CheckoutOrderCommand` | Orchestrated checkout workflow |
| | `ConfirmOrderPaymentCommand` | Transition order to Confirmed |
| | `StartOrderProcessingCommand` | Transition order to Processing |
| | `ShipOrderCommand` | Transition order to Shipped |
| | `DeliverOrderCommand` | Transition order to Delivered |
| | `CancelOrderCommand` | Cancel order |
| | `RequestOrderRefundCommand` | Request order refund |
| | `CompleteOrderRefundCommand` | Process completed refund |
| | `GetOrderByIdQuery` | Retrieve order by ID |
| | `GetOrderByNumberQuery` | Retrieve order by order number |
| | `GetOrdersByCustomerIdQuery` | List customer orders |
| | `SearchOrdersQuery` | Admin search and filter orders |
| **Payments** | `AuthorizePaymentCommand` | Authorize payment charge |
| | `CapturePaymentCommand` | Capture authorized payment |
| | `FailPaymentCommand` | Record payment failure |
| | `RefundPaymentCommand` | Process full/partial refund |
| | `GetPaymentByIdQuery` | Retrieve payment by ID |
| | `GetPaymentByOrderIdQuery` | Retrieve payment for order |
| | `GetPaymentByIdempotencyKeyQuery` | Find payment by idempotency key |
| **Shipments** | `CreateShipmentLabelCommand` | Generate label & set tracking number |
| | `MarkShipmentInTransitCommand` | Transition shipment to InTransit |
| | `MarkShipmentOutForDeliveryCommand` | Transition shipment to OutForDelivery |
| | `MarkShipmentDeliveredCommand` | Transition shipment to Delivered |
| | `MarkShipmentFailedCommand` | Record shipment failure |
| | `GetShipmentByIdQuery` | Retrieve shipment by ID |
| | `GetShipmentByOrderIdQuery` | Retrieve shipment for order |
| | `TrackShipmentQuery` | Fetch carrier tracking details |
| **Promotions** | `CreatePromotionCommand` | Create promotion with validity range |
| | `UpdatePromotionCommand` | Update promotion rules |
| | `ApplyPromotionCodeCommand` | Validate and calculate promotion discount |
| | `RecordPromotionUsageCommand` | Increment usage & check cap |
| | `DeactivatePromotionCommand` | Deactivate promotion |
| | `GetPromotionByIdQuery` | Retrieve promotion by ID |
| | `GetPromotionByCodeQuery` | Retrieve promotion by code |
| | `ListActivePromotionsQuery` | List currently valid promotions |
| **Returns** | `SubmitReturnRequestCommand` | Customer submits return/exchange request |
| | `ApproveReturnRequestCommand` | Admin approves request |
| | `RejectReturnRequestCommand` | Admin rejects request |
| | `MarkReturnItemsReceivedCommand` | Admin marks returned items received |
| | `CompleteReturnRefundCommand` | Process refund & restock items |
| | `CompleteExchangeReplacementCommand` | Create replacement order & restock originals |
| | `GetReturnRequestByIdQuery` | Retrieve return request by ID |
| | `GetReturnRequestsByOrderIdQuery` | List return requests for order |
| | `ListPendingReturnRequestsQuery` | Admin queue of pending return requests |
| **Analytics** | `CaptureAnalyticsEventCommand` | Capture consent-aware event |
| | `ForwardAnalyticsEventsCommand` | Forward events to external provider |
| | `GetCustomerAnalyticsHistoryQuery` | Retrieve customer analytics history |
| **VendorSettings** | `PatchVendorRuntimeSettingsCommand` | Patch runtime settings & invalidate cache |
| | `GetVendorConfigQuery` | Retrieve unified 3-tier vendor config |
| | `GetVendorConfigSchemaQuery` | Retrieve JSON schema for vendor config |

#### Explicit Orchestration Workflows
- **FR-007**: `CheckoutOrderCommand` MUST execute the following 10-step atomic orchestration:
  1. Validate cart exists, is active, and contains at least 1 item.
  2. Verify stock availability for every variant line item in the cart.
  3. Evaluate optional discount code via `Promotion` aggregate if present.
  4. Calculate sales tax via `ITaxCalculator` for the destination address.
  5. Open an atomic database transaction via `IUnitOfWork`.
  6. Instantiate `Order` and `Payment` domain aggregates.
  7. Decrement variant stock quantities for all ordered line items.
  8. Record promotion usage count if a discount code was applied.
  9. Mark the guest or customer cart cleared / converted.
  10. Commit the transaction, then initiate payment authorization with `IPaymentGateway`.
- **FR-008**: Return and Exchange requests MUST follow a 5-step lifecycle orchestration:
  1. **Submission**: Customer invokes `SubmitReturnRequestCommand` creating `ReturnRequest` in `Pending` status.
  2. **Approval**: Admin invokes `ApproveReturnRequestCommand` setting resolution to `Refund` or `Exchange`.
  3. **Receipt**: Admin invokes `MarkReturnItemsReceivedCommand` when physical items arrive at warehouse.
  4. **Return Completion**: `CompleteReturnRefundCommand` issues payment refund via `IPaymentGateway` and restocks returned variant items.
  5. **Exchange Completion**: `CompleteExchangeReplacementCommand` creates a new replacement `Order` and restocks original variant items.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of Application layer handlers return `Result<T>` and zero uncaught business logic exceptions leak to callers.
- **SC-002**: 100% of invalid command payloads return HTTP 422 with structured field-level validation errors.
- **SC-003**: 100% of write commands run within an atomic `IUnitOfWork` database transaction that rolls back completely on any failure.
- **SC-004**: Duplicate idempotent requests (same idempotency key) return the cached result without re-executing handler logic or DB writes.
- **SC-005**: 100% of pipeline executions taking >500ms produce a logged performance warning.
- **SC-006**: Application unit test coverage reaches or exceeds 85% line coverage using in-memory mocks/fakes for repositories.

---

## Assumptions

- Application layer uses MediatR 12 for CQRS dispatch and FluentValidation 11 for input validation.
- All DTOs are defined as immutable C# `record` types.
- Password hashing and JWT generation are delegated to `ITokenService` abstractions implemented in Infrastructure.
- The 5-stage pipeline behavior chain is registered in DI such that `LoggingBehavior` is the outermost wrapper and `PerformanceBehavior` / `TransactionBehavior` wrap handler invocation directly.
