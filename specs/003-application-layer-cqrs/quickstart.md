# Quickstart Validation Guide: Application Layer CQRS & Pipeline Architecture

**Feature**: 003-application-layer-cqrs
**Branch**: `003-application-layer-cqrs`

This guide describes how to validate that the Application layer implementation is correct end-to-end using unit test suites and in-memory fakes. It references [data-model.md](../data-model.md) and [contracts/](../contracts/) for structural details.

---

## Prerequisites

| Requirement | Version / Detail |
|-------------|-----------------|
| .NET SDK | 9.0 (latest) |
| `Vendor.Application` project | Dependencies: `Vendor.Domain`, `MediatR` 12.x, `FluentValidation` 11.x |
| `Vendor.Application.Tests` project | xUnit 2.x, FluentAssertions, NSubstitute / in-memory fakes |

---

## Setup

```powershell
# From repo root — build the Application project
dotnet build src/Vendor.Application/Vendor.Application.csproj

# Build the Application test project
dotnet build tests/Vendor.Application.Tests/Vendor.Application.Tests.csproj
```

---

## Validation Scenarios

Run all application layer tests:
```powershell
dotnet test tests/Vendor.Application.Tests/ --logger "console;verbosity=normal"
```

### Scenario 1 — Pipeline Behavior Chain Execution & Short-Circuiting

**Test class**: `PipelineBehaviorTests`

| Step | Command / Assertion |
|------|---------------------|
| Send command with invalid payload (e.g. negative price) | `ValidationBehavior` short-circuits execution |
| Assert result type & code | `result.IsFailure == true`, `result.Error` is `ValidationError` (maps to HTTP 422) |
| Verify transaction behavior | `IUnitOfWork.BeginTransactionAsync()` was **NOT** invoked |

---

### Scenario 2 — Idempotency De-duplication

**Test class**: `PipelineBehaviorTests`

| Step | Command / Assertion |
|------|---------------------|
| Send idempotent command (`IIdempotentRequest`) key `"IDEMP-001"` | First execution handles request and stores result in `IIdempotencyStore` |
| Resend command with key `"IDEMP-001"` | `IdempotencyBehavior` short-circuits execution |
| Assert behavior | Returns cached result without executing handler logic or starting DB transaction |

---

### Scenario 3 — Transaction Rollback on Failure

**Test class**: `PipelineBehaviorTests`

| Step | Command / Assertion |
|------|---------------------|
| Execute command where handler returns `Result.Failure(...)` | `TransactionBehavior` catches failure |
| Assert UnitOfWork calls | `IUnitOfWork.RollbackAsync()` is invoked; `CommitAsync()` is **NOT** invoked |

---

### Scenario 4 — Checkout Orchestration Flow (Successful Checkout)

**Test class**: `CheckoutOrchestrationTests`

| Step | Assertion |
|------|-----------|
| Populate active cart with items, valid discount code, address | Cart is active |
| Execute `CheckoutOrderCommand` | Handled successfully |
| Assert database state | `Order` created (`Pending`), `Payment` created (`Pending`), variant stock decremented, promotion usage recorded, cart marked `ConvertedToOrder` |
| Assert transaction commit | `IUnitOfWork.CommitAsync()` invoked **before** `IPaymentGateway.AuthorizeAsync()` |
| Assert gateway invocation | `IPaymentGateway.AuthorizeAsync()` called with payment ID & total amount |

---

### Scenario 5 — Checkout Orchestration Flow (Insufficient Stock Rollback)

**Test class**: `CheckoutOrchestrationTests`

| Step | Assertion |
|------|-----------|
| Populate cart with item where requested qty > available stock | Initial cart state |
| Execute `CheckoutOrderCommand` | Fails before committing transaction |
| Assert result | `Result.Failure` returned with `Stock.Insufficient` error code |
| Assert DB state | No `Order` created, no `Payment` created, stock unchanged |

---

### Scenario 6 — Return / Exchange Multi-Stage Workflow

**Test class**: `ReturnWorkflowTests`

| Step | Assertion |
|------|-----------|
| Customer submits return request | `SubmitReturnRequestCommand` creates `ReturnRequest` (`Status = Pending`) |
| Admin approves request | `ApproveReturnRequestCommand` sets `RequestedResolution = Refund` (`Status = Approved`) |
| Admin marks items received | `MarkReturnItemsReceivedCommand` sets `Status = ItemsReceived` |
| Complete return | `CompleteReturnRefundCommand` calls `IPaymentGateway.RefundAsync`, calls `variant.AddStock()`, sets `Status = Returned` |
| Complete exchange | `CompleteExchangeReplacementCommand` creates replacement `Order`, calls `variant.AddStock()` for original items, sets `Status = Exchanged` |

---

## Coverage Gate

Verify Application layer coverage meets ≥ 85%:

```powershell
dotnet test tests/Vendor.Application.Tests/ \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage

reportgenerator -reports:"coverage/**/coverage.cobertura.xml" \
  -targetdir:"coverage/report" \
  -reporttypes:TextSummary

Get-Content coverage/report/Summary.txt
```

**Expected**: `Line coverage: ≥ 85.0%` for `Vendor.Application` assembly.

---

## Next Steps

After all 6 validation scenarios pass and coverage ≥ 85%:

```bash
/speckit-tasks
```
