# Research: Application Layer CQRS & Pipeline Architecture

**Feature**: 003-application-layer-cqrs
**Date**: 2026-07-25

---

## R1: MediatR 12 Pipeline Behavior Ordering & Pipeline Short-Circuiting

**Decision**: Register the 5 pipeline behaviors in DI in exact execution order:
1. `LoggingBehavior<TRequest, TResponse>`
2. `ValidationBehavior<TRequest, TResponse>`
3. `IdempotencyBehavior<TRequest, TResponse>`
4. `TransactionBehavior<TRequest, TResponse>`
5. `PerformanceBehavior<TRequest, TResponse>`

**Rationale**:
- `LoggingBehavior` is outermost so it logs total request lifecycle duration, including validation failures and cached idempotency hits.
- `ValidationBehavior` executes before `IdempotencyBehavior` and `TransactionBehavior` so invalid payloads fail fast (422) without opening DB transactions or performing cache lookups.
- `IdempotencyBehavior` checks `IIdempotencyStore` before `TransactionBehavior` so duplicate requests return cached results without starting a database transaction.
- `TransactionBehavior` wraps only commands (`ICommand` marker) in `IUnitOfWork.BeginTransactionAsync()` / `CommitAsync()`, rolling back automatically on exception or `Result.Failure`.
- `PerformanceBehavior` sits closest to the handler to measure pure execution duration and emit a warning if total duration > 500ms.

**Pipeline Short-Circuiting Pattern**:
To short-circuit in MediatR 12 without throwing exceptions:
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

        if (failures.Count != 0)
        {
            var errors = failures
                .GroupBy(f => f.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

            // Reflection-free dynamic Result creation for generic TResponse
            return (TResponse)(object)Result.Failure(new ValidationError(errors));
        }

        return await next();
    }
}
```

---

## R2: `Result<T>` and Error Variant Taxonomy in Pure C#

**Decision**: Implement immutable `Result` and `Result<T>` structs with a hierarchy of typed `Error` records:

```csharp
public abstract record Error(string Code, string Message);

public record NotFoundError(string EntityName, object Key)
    : Error("NOT_FOUND", $"{EntityName} with key '{Key}' was not found.");

public record ValidationError(IDictionary<string, string[]> Errors)
    : Error("VALIDATION_ERROR", "One or more validation failures occurred.");

public record ConflictError(string Code, string Message)
    : Error(Code, Message);

public record UnauthorizedError(string Message = "Authentication credentials are required.")
    : Error("UNAUTHORIZED", Message);

public record ForbiddenError(string Message = "You do not have permission to perform this action.")
    : Error("FORBIDDEN", Message);
```

**HTTP Status Mapping in API Layer**:
- `Result.Success` → HTTP 200 / 201
- `Result.Failure(NotFoundError)` → HTTP 404
- `Result.Failure(ValidationError)` → HTTP 422 (includes `Errors` dictionary in JSON payload)
- `Result.Failure(ConflictError)` → HTTP 409
- `Result.Failure(UnauthorizedError)` → HTTP 401
- `Result.Failure(ForbiddenError)` → HTTP 403
- `Result.Failure(Error)` → HTTP 400 (includes `Code` and `Message`)

**Rationale**: Typed error records enable compile-time type safety and pattern matching (`result.Error switch { ... }`) in the API layer while guaranteeing that business logic never relies on costly thrown exceptions.

---

## R3: Checkout Orchestration Transaction Boundary Pattern

**Decision**: Local database commit happens **BEFORE** calling external payment gateways (`IPaymentGateway.AuthorizeAsync`).

**Orchestration Sequence**:
1. Validate cart exists, is active, and contains at least 1 line.
2. Verify stock for every ordered variant (`variant.StockQuantity >= qty`).
3. Calculate tax via `ITaxCalculator`.
4. Evaluate discount via `Promotion` aggregate if coupon code present.
5. Open database transaction (`IUnitOfWork.BeginTransactionAsync`).
6. Create `Order` (status: `Pending`) and `Payment` (status: `Pending`).
7. Decrement variant stock quantities (`variant.DeductStock`).
8. Record promotion usage count if applied.
9. Clear cart / set status `ConvertedToOrder`.
10. **Commit database transaction** (`IUnitOfWork.CommitAsync`).
11. **Initiate Payment Authorization** (`IPaymentGateway.AuthorizeAsync`).
    - If authorization succeeds: call `payment.Capture()`, transition order to `Confirmed`, commit.
    - If authorization fails: call `payment.Fail(reason)`, transition order to `Cancelled` or `PendingPayment`, commit.

**Rationale**: Committing the local DB transaction first prevents long-running external HTTP calls to Stripe/PayPal from holding DB locks open. If Kestrel crashes during the gateway call, the order remains persisted in `Pending` state and can be safely retried or auto-expired.

---

## R4: Return / Exchange Multi-Stage Workflow Architecture

**Decision**: Structure the Return/Exchange process as 5 explicit application commands corresponding to warehouse and administrative domain checkpoints:

1. `SubmitReturnRequestCommand`: Customer submits return/exchange items. Validates order is `Delivered`. Instantiates `ReturnRequest` aggregate (`Status = Pending`).
2. `ApproveReturnRequestCommand`: Admin approves request, setting `RequestedResolution` to `Refund` or `Exchange` (`Status = Approved`).
3. `MarkReturnItemsReceivedCommand`: Warehouse receives physical items. Validates request is `Approved` (`Status = ItemsReceived`).
4. `CompleteReturnRefundCommand`: Invokes `IPaymentGateway.RefundAsync`, calls `variant.AddStock()` for returned items, transitions `ReturnRequest` to `Returned`.
5. `CompleteExchangeReplacementCommand`: Creates a new replacement `Order`, calls `variant.AddStock()` for original returned items, transitions `ReturnRequest` to `Exchanged`.

**Rationale**: Multi-stage state machine prevents issuing refunds before physical goods are received at the warehouse and enforces strict separation between refund and exchange fulfillment branches.
