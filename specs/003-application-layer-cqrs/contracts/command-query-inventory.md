# Contract: Complete Command & Query Inventory

**Feature**: 003-application-layer-cqrs
**Total Handlers**: 51 (36 Commands, 15 Queries across 11 modules)

---

## Command Marker Interfaces
- `ICommand<TResponse> : IRequest<Result<TResponse>>`
- `ICommand : IRequest<Result>`
- `IIdempotentRequest` — marker interface carrying `string IdempotencyKey` property

---

## 1. Auth Module (6 Commands, 2 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `RegisterCustomerCommand` | `Email, Password, FirstName, LastName` | `AuthResponseDto` | No |
| Command | `LoginWithPasswordCommand` | `Email, Password` | `AuthResponseDto` | No |
| Command | `LoginWithOAuthCommand` | `Provider ("google"/"facebook"), IdToken` | `AuthResponseDto` | No |
| Command | `RefreshTokenCommand` | `RefreshToken` | `AuthResponseDto` | No |
| Command | `RevokeTokenCommand` | `RefreshToken` | `void` | Yes |
| Command | `ChangePasswordCommand` | `CurrentPassword, NewPassword` | `void` | No |
| Query | `GetCurrentUserProfileQuery` | - | `CustomerDto` | N/A |
| Query | `ValidateTokenQuery` | `Token` | `bool` | N/A |

---

## 2. Products Module (9 Commands, 4 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `CreateProductCommand` | `Name, Slug, BasePrice, Currency, LowStockThreshold, Description` | `ProductDto` | No |
| Command | `UpdateProductCommand` | `ProductId, Name, Slug, BasePrice, Description` | `ProductDto` | No |
| Command | `ActivateProductCommand` | `ProductId` | `void` | Yes |
| Command | `DeactivateProductCommand` | `ProductId, Reason` | `void` | Yes |
| Command | `AddProductVariantCommand` | `ProductId, Sku, PriceAdjustment, StockQuantity, Weight, Dimensions` | `ProductVariantDto` | No |
| Command | `UpdateProductVariantCommand` | `ProductVariantId, PriceAdjustment, StockQuantity, Weight, Dimensions` | `ProductVariantDto` | No |
| Command | `DeleteProductVariantCommand` | `ProductVariantId` | `void` | Yes |
| Command | `AddProductImageCommand` | `ProductId, ImageUrl` | `void` | Yes |
| Command | `RemoveProductImageCommand` | `ProductId, ImageUrl` | `void` | Yes |
| Query | `GetProductByIdQuery` | `ProductId` | `ProductDto` | N/A |
| Query | `GetProductBySlugQuery` | `Slug` | `ProductDto` | N/A |
| Query | `SearchProductsQuery` | `SearchTerm, Category, MinPrice, MaxPrice, PageIndex, PageSize` | `PagedList<ProductDto>` | N/A |
| Query | `GetProductVariantsQuery` | `ProductId` | `IReadOnlyList<ProductVariantDto>` | N/A |

---

## 3. Customers Module (6 Commands, 3 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `RegisterGuestCustomerCommand` | `Email, FirstName, LastName` | `CustomerDto` | No |
| Command | `ConvertGuestToRegisteredCommand` | `CustomerId, Email, Password` | `CustomerDto` | No |
| Command | `UpdateCustomerProfileCommand` | `CustomerId, FirstName, LastName` | `CustomerDto` | No |
| Command | `AddShippingAddressCommand` | `CustomerId, Street, City, State, ZipCode, CountryCode` | `AddressDto` | No |
| Command | `RemoveShippingAddressCommand` | `CustomerId, AddressIndex` | `void` | Yes |
| Command | `UpdateAnalyticsConsentCommand` | `CustomerId, Granted` | `void` | Yes |
| Query | `GetCustomerByIdQuery` | `CustomerId` | `CustomerDto` | N/A |
| Query | `GetCustomerByEmailQuery` | `Email` | `CustomerDto` | N/A |
| Query | `GetCustomerOrderHistoryQuery` | `CustomerId, PageIndex, PageSize` | `PagedList<OrderDto>` | N/A |

---

## 4. Cart Module (9 Commands, 3 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `CreateCartCommand` | `CustomerId?, SessionId?` | `CartDto` | No |
| Command | `AddCartItemCommand` | `CartId, VariantId, Quantity` | `CartDto` | No |
| Command | `UpdateCartItemQuantityCommand` | `CartId, VariantId, Quantity` | `CartDto` | No |
| Command | `RemoveCartItemCommand` | `CartId, VariantId` | `CartDto` | Yes |
| Command | `ApplyCartDiscountCodeCommand` | `CartId, DiscountCode` | `CartDto` | Yes |
| Command | `RemoveCartDiscountCodeCommand` | `CartId` | `CartDto` | Yes |
| Command | `ClearCartCommand` | `CartId` | `void` | Yes |
| Command | `MergeGuestCartCommand` | `GuestCartId, CustomerCartId` | `CartDto` | Yes |
| Command | `ProcessCartAbandonmentCommand` | `TimeoutHours` | `int (CountMarked)` | Yes |
| Query | `GetCartByIdQuery` | `CartId` | `CartDto` | N/A |
| Query | `GetCartByCustomerIdQuery` | `CustomerId` | `CartDto` | N/A |
| Query | `GetCartBySessionIdQuery` | `SessionId` | `CartDto` | N/A |

---

## 5. Orders Module (9 Commands, 4 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `PlaceOrderCommand` | `CustomerId, ShippingAddress, LineItems` | `OrderDto` | Yes |
| Command | `CheckoutOrderCommand` | `CartId, ShippingAddress, PaymentMethodDetails, IdempotencyKey` | `OrderDto` | Yes |
| Command | `ConfirmOrderPaymentCommand` | `OrderId` | `OrderDto` | Yes |
| Command | `StartOrderProcessingCommand` | `OrderId` | `OrderDto` | Yes |
| Command | `ShipOrderCommand` | `OrderId, CarrierCode, TrackingNumber` | `OrderDto` | Yes |
| Command | `DeliverOrderCommand` | `OrderId` | `OrderDto` | Yes |
| Command | `CancelOrderCommand` | `OrderId, Reason` | `OrderDto` | Yes |
| Command | `RequestOrderRefundCommand` | `OrderId, Reason` | `OrderDto` | Yes |
| Command | `CompleteOrderRefundCommand` | `OrderId` | `OrderDto` | Yes |
| Query | `GetOrderByIdQuery` | `OrderId` | `OrderDto` | N/A |
| Query | `GetOrderByNumberQuery` | `OrderNumber` | `OrderDto` | N/A |
| Query | `GetOrdersByCustomerIdQuery` | `CustomerId, PageIndex, PageSize` | `PagedList<OrderDto>` | N/A |
| Query | `SearchOrdersQuery` | `Status?, CustomerId?, FromDate?, ToDate?, PageIndex, PageSize` | `PagedList<OrderDto>` | N/A |

---

## 6. Payments Module (4 Commands, 3 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `AuthorizePaymentCommand` | `OrderId, Amount, Currency, IdempotencyKey` | `PaymentDto` | Yes |
| Command | `CapturePaymentCommand` | `PaymentId, GatewayTransactionId` | `PaymentDto` | Yes |
| Command | `FailPaymentCommand` | `PaymentId, Reason` | `PaymentDto` | Yes |
| Command | `RefundPaymentCommand` | `PaymentId, RefundAmount, IdempotencyKey` | `PaymentDto` | Yes |
| Query | `GetPaymentByIdQuery` | `PaymentId` | `PaymentDto` | N/A |
| Query | `GetPaymentByOrderIdQuery` | `OrderId` | `PaymentDto` | N/A |
| Query | `GetPaymentByIdempotencyKeyQuery` | `IdempotencyKey` | `PaymentDto` | N/A |

---

## 7. Shipments Module (5 Commands, 3 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `CreateShipmentLabelCommand` | `OrderId, CarrierCode, EstimatedDelivery` | `ShipmentDto` | Yes |
| Command | `MarkShipmentInTransitCommand` | `ShipmentId` | `ShipmentDto` | Yes |
| Command | `MarkShipmentOutForDeliveryCommand` | `ShipmentId` | `ShipmentDto` | Yes |
| Command | `MarkShipmentDeliveredCommand` | `ShipmentId` | `ShipmentDto` | Yes |
| Command | `MarkShipmentFailedCommand` | `ShipmentId, Reason` | `ShipmentDto` | Yes |
| Query | `GetShipmentByIdQuery` | `ShipmentId` | `ShipmentDto` | N/A |
| Query | `GetShipmentByOrderIdQuery` | `OrderId` | `ShipmentDto` | N/A |
| Query | `TrackShipmentQuery` | `TrackingNumber, CarrierCode` | `ShipmentTrackingInfo` | N/A |

---

## 8. Promotions Module (5 Commands, 3 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `CreatePromotionCommand` | `Code, DiscountType, Value, MaxDiscountAmount, StartUtc, EndUtc, MaxUsageCount` | `PromotionDto` | No |
| Command | `UpdatePromotionCommand` | `PromotionId, Value, MaxDiscountAmount, StartUtc, EndUtc, MaxUsageCount` | `PromotionDto` | No |
| Command | `ApplyPromotionCodeCommand` | `Code, OrderSubtotal` | `Money` | Yes |
| Command | `RecordPromotionUsageCommand` | `PromotionId` | `void` | No |
| Command | `DeactivatePromotionCommand` | `PromotionId` | `void` | Yes |
| Query | `GetPromotionByIdQuery` | `PromotionId` | `PromotionDto` | N/A |
| Query | `GetPromotionByCodeQuery` | `Code` | `PromotionDto` | N/A |
| Query | `ListActivePromotionsQuery` | - | `IReadOnlyList<PromotionDto>` | N/A |

---

## 9. Returns Module (6 Commands, 3 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `SubmitReturnRequestCommand` | `OrderId, CustomerId, Reason, Items` | `ReturnRequestDto` | No |
| Command | `ApproveReturnRequestCommand` | `ReturnRequestId, ResolutionType ("Refund"/"Exchange")` | `ReturnRequestDto` | Yes |
| Command | `RejectReturnRequestCommand` | `ReturnRequestId` | `ReturnRequestDto` | Yes |
| Command | `MarkReturnItemsReceivedCommand` | `ReturnRequestId` | `ReturnRequestDto` | Yes |
| Command | `CompleteReturnRefundCommand` | `ReturnRequestId` | `ReturnRequestDto` | Yes |
| Command | `CompleteExchangeReplacementCommand` | `ReturnRequestId` | `ReturnRequestDto` | Yes |
| Query | `GetReturnRequestByIdQuery` | `ReturnRequestId` | `ReturnRequestDto` | N/A |
| Query | `GetReturnRequestsByOrderIdQuery` | `OrderId` | `IReadOnlyList<ReturnRequestDto>` | N/A |
| Query | `ListPendingReturnRequestsQuery` | `PageIndex, PageSize` | `PagedList<ReturnRequestDto>` | N/A |

---

## 10. Analytics Module (2 Commands, 1 Query)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `CaptureAnalyticsEventCommand` | `CustomerId?, EventType, Payload, ConsentGranted` | `AnalyticsEventDto` | No |
| Command | `ForwardAnalyticsEventsCommand` | `EventIds` | `int (CountForwarded)` | Yes |
| Query | `GetCustomerAnalyticsHistoryQuery` | `CustomerId, PageIndex, PageSize` | `PagedList<AnalyticsEventDto>` | N/A |

---

## 11. VendorSettings Module (1 Command, 2 Queries)

| Type | Name | Parameters | Return Type | Idempotent |
|------|------|------------|-------------|------------|
| Command | `PatchVendorRuntimeSettingsCommand` | `RuntimeConfigPatchJson` | `VendorConfigDto` | Yes |
| Query | `GetVendorConfigQuery` | - | `VendorConfigDto` | N/A |
| Query | `GetVendorConfigSchemaQuery` | - | `string (JSON Schema)` | N/A |
