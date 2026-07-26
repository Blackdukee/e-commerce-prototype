# Implementation Plan: Customer Account Management

**Branch**: `007-customer-account-management` | **Date**: 2026-07-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-customer-account-management/spec.md`

## Summary

Extend the existing `Customer` aggregate root in `Vendor.Domain` with `Role` (`CustomerRole`) and `Status` (`CustomerStatus`), suspension metadata, and role-change audit metadata directly on the aggregate. Extend `ICustomerRepository` for paginated filtering and audit log queries. Reuse the existing transactional outbox, `Result<T>` handlers, and `"auth"` rate-limiting policy. Enforce `SuperAdmin` authority checks within command handlers to prevent authorization bypass.

## Technical Context

**Language/Version**: C# 13 / .NET 9
**Primary Dependencies**: MediatR 12.x, FluentValidation 11.x, EF Core 9.0 (MSSQL)
**Storage**: Microsoft SQL Server (`Customers`, `CustomerAuditLogs`, `OutboxMessages`)
**Testing**: xUnit, FluentAssertions, Moq, Testcontainers (MSSQL), WebApplicationFactory
**Target Platform**: ASP.NET Core 9 Minimal APIs on Windows / Linux containers
**Project Type**: Clean Architecture Web Service
**Performance Goals**: Sub-200ms p95 for paginated customer listings up to 100,000 records
**Constraints**: Zero external NuGet packages in Domain; Result-oriented handlers (`Result<T>`); Outbox for domain events; SuperAdmin checks in command handlers; reuse `"auth"` rate limit policy for promote/demote endpoints.
**Scale/Scope**: Single-tenant clone-per-vendor e-commerce platform

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Compliance Verification |
|---|---|---|
| **I. Clean Architecture** | **PASS** | `CustomerRole` and `CustomerStatus` enums and methods added to `Vendor.Domain` with zero external NuGet packages. Handlers live in `Vendor.Application`. Persistence mapping in `Vendor.Infrastructure`. Endpoints in `Vendor.Api`. |
| **II. Result-Oriented Handlers** | **PASS** | All new handlers (`SuspendCustomerCommandHandler`, `ReactivateCustomerCommandHandler`, `PromoteCustomerCommandHandler`, `DemoteCustomerCommandHandler`, etc.) return `Result<T>` or `Result`. No thrown domain exceptions. |
| **III. MSSQL via EF Core** | **PASS** | EF Core entity configurations in `Vendor.Infrastructure.Persistence.Configurations`. Enums mapped as string columns. `CustomerAuditLogs` mapped via EF Core. |
| **IV. Clone-Per-Vendor Isolation** | **PASS** | SuperAdmin seed configuration defined in `vendor.config.json` / boot settings. Zero C# code changes required for onboarding vendors. |
| **V. Secrets Management** | **PASS** | No secrets introduced or exposed in source code or config. |
| **VI. Transactional Outbox** | **PASS** | `CustomerSuspendedEvent`, `CustomerReactivatedEvent`, `CustomerRoleChangedEvent` saved to `OutboxMessages` in the same EF transaction. |
| **VII. Test Coverage Targets** | **PASS** | Unit tests in `Vendor.Domain.Tests` (≥90%), handler tests in `Vendor.Application.Tests` (≥85%), persistence in `Vendor.Infrastructure.Tests` (≥70%), and integration tests in `Vendor.Api.Tests` (≥75%). |

## Project Structure

### Documentation (this feature)

```text
specs/007-customer-account-management/
├── plan.md              # Implementation plan (/speckit-plan output)
├── research.md          # Phase 0 output (/speckit-plan output)
├── data-model.md        # Phase 1 output (/speckit-plan output)
├── quickstart.md        # Phase 1 output (/speckit-plan output)
├── contracts/           # Phase 1 output (/speckit-plan output)
│   └── admin-customer-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── Vendor.Domain/
│   └── Aggregates/Customer/
│       ├── Customer.cs                  # Extended with Role, Status, Suspension & Role Audit metadata
│       ├── CustomerRole.cs              # Enum: Customer, Admin, SuperAdmin
│       └── CustomerStatus.cs            # Enum: Active, Suspended
│   └── Events/
│       ├── CustomerSuspendedEvent.cs
│       ├── CustomerReactivatedEvent.cs
│       └── CustomerRoleChangedEvent.cs
│   └── Interfaces/Repositories/
│       └── ICustomerRepository.cs       # Extended with GetPagedAsync & GetAuditLogsAsync
│
├── Vendor.Application/
│   └── Modules/Customers/
│       ├── Commands/
│       │   ├── SuspendCustomerCommand.cs
│       │   ├── ReactivateCustomerCommand.cs
│       │   ├── PromoteCustomerCommand.cs
│       │   └── DemoteCustomerCommand.cs
│       ├── Queries/
│       │   ├── GetAdminCustomersQuery.cs
│       │   ├── GetCustomerDetailQuery.cs
│       │   └── GetCustomerAuditLogsQuery.cs
│       ├── DTOs/
│       │   ├── AdminCustomerDto.cs
│       │   ├── CustomerDetailDto.cs
│       │   └── CustomerAuditLogDto.cs
│       └── CustomerHandlers.cs          # CQRS Handlers enforcing SuperAdmin checks & Result<T>
│
├── Vendor.Infrastructure/
│   └── Persistence/
│       ├── Configurations/
│       │   ├── CustomerConfiguration.cs # Updated EF Core mapping
│       │   └── CustomerAuditLogConfiguration.cs
│       ├── Repositories/
│       │   └── CustomerRepository.cs    # Extended EF Core implementation
│       └── Migrations/                  # EF Core database migration
│
└── Vendor.Api/
    └── Endpoints/
        └── AdminCustomerEndpoints.cs    # Admin endpoints with "auth" rate limiter on promote/demote

tests/
├── Vendor.Domain.Tests/Aggregates/CustomerTests.cs
├── Vendor.Application.Tests/Handlers/CustomerHandlerTests.cs
├── Vendor.Infrastructure.Tests/Persistence/CustomerRepositoryTests.cs
└── Vendor.Api.Tests/Integration/AdminCustomerEndpointsTests.cs
```

## Complexity Tracking

> **No constitution check violations present.** All additions are purely additive extensions of existing Clean Architecture layers.
