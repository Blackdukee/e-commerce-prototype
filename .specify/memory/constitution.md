<!-- Sync Impact Report
  Version change: (none) → 1.0.0
  Modified principles: N/A (initial fill)
  Added sections:
    - Core Principles (7 principles)
    - Technology & Infrastructure Constraints
    - Quality Gates & Development Workflow
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md        ✅ reviewed — no update needed
    - .specify/templates/spec-template.md         ✅ reviewed — no update needed
    - .specify/templates/tasks-template.md        ✅ reviewed — no update needed
  Follow-up TODOs: none
-->

# E-Commerce Platform Constitution

## Core Principles

### I. Clean Architecture — Strict Dependency Direction

The solution MUST follow Clean Architecture with an inward-only dependency
rule: Domain → Application → Infrastructure → API. Each layer may depend
only on the layer directly inside it.

- The **Domain** layer MUST have **zero external NuGet package references**.
  Only `netstandard2.1` / `net9.0` BCL types are permitted.
- The **Application** layer may reference MediatR and FluentValidation
  abstractions but MUST NOT reference any infrastructure package (EF Core,
  HTTP clients, caching SDKs).
- The **Infrastructure** layer owns all concrete implementations: EF Core
  DbContext, payment gateway adapters, shipping adapters, email providers,
  cache providers, and the transactional outbox dispatcher.
- The **API** layer is the composition root; it wires DI and exposes
  Minimal API endpoints. It MUST NOT contain business logic.

**Rationale**: Dependency inversion keeps the domain portable, testable
without infrastructure, and immune to vendor-SDK churn.

### II. Result-Oriented Command/Query Handlers

Every `IRequestHandler<TRequest, TResponse>` in the Application layer
MUST return `Result<T>` (or `Result` for void operations).

- Business-rule violations MUST be communicated via typed error variants
  inside `Result<T>`, never via thrown exceptions.
- Exceptions are reserved exclusively for unexpected infrastructure
  failures (network timeouts, database connectivity loss).
- Pipeline behaviors (validation, logging, transaction) MUST catch and
  wrap infrastructure exceptions into `Result.Failure(...)` at the
  boundary before the response reaches the API layer.

**Rationale**: Deterministic control flow eliminates hidden throw-paths,
makes error handling explicit in every caller, and simplifies testing.

### III. MSSQL via EF Core — Owned Types for Value Objects

Microsoft SQL Server is the sole supported RDBMS. All data access goes
through Entity Framework Core.

- Value objects (e.g., `Money`, `Address`, `PhoneNumber`, `Email`) MUST
  be mapped as EF Core **owned types** on the parent entity.
- Owned types MUST NOT produce separate database tables; they MUST be
  stored as columns on the owning entity's table.
- Each aggregate root MUST have a dedicated `IEntityTypeConfiguration<T>`
  class inside the Infrastructure layer.
- Migrations MUST be idempotent and vendor-agnostic: no vendor-specific
  seed data inside migration files.

**Rationale**: Owned types keep the relational model flat and
query-friendly while preserving rich domain semantics in C#.

### IV. Clone-Per-Vendor Isolation (NON-NEGOTIABLE)

The platform operates as a single-tenant, clone-per-vendor system.
Cloning the repository for a new vendor MUST require editing **only**:

1. `config/vendor.config.json` — vendor identity, branding, locale,
   payment/shipping provider selection, feature flags.
2. `theme/` — CSS variables, logo assets, email templates.

- **Zero C# code changes** are permitted for vendor onboarding.
- **Zero React/frontend code changes** are permitted.
- All vendor-variable behavior MUST be driven by configuration, feature
  flags, or adapter selection — never by conditional code branches keyed
  on vendor identity.
- If a design requires a code change to onboard a new vendor, the design
  is considered **FAILED** and MUST be reworked.

**Rationale**: True clone-per-vendor eliminates merge conflicts across
vendor forks and guarantees that upstream improvements propagate to every
deployment without manual intervention.

### V. Secrets Management — Reference-Only Policy

Raw secret values (API keys, connection strings, webhook secrets) MUST
NEVER appear in configuration files, source code, environment variable
defaults, or CI/CD pipeline definitions.

- All secrets in `vendor.config.json` MUST use one of the following
  reference prefixes:
  - `ref:env:<VAR_NAME>` — resolve from environment variable.
  - `ref:vault:<PATH>` — resolve from HashiCorp Vault.
  - `ref:aws-ssm:<PARAMETER_PATH>` — resolve from AWS Systems Manager
    Parameter Store.
- The Infrastructure layer MUST include a `SecretResolver` service that
  resolves references at boot time and caches decrypted values in memory.
- Application startup MUST fail-fast with a descriptive error if any
  secret reference cannot be resolved.

**Rationale**: Reference-only secrets prevent accidental leakage in
version control, logs, and error reports while supporting heterogeneous
secret backends across deployment environments.

### VI. Domain Events via Transactional Outbox

Domain events MUST be published through a transactional outbox pattern,
never by dispatching MediatR notifications directly within the same
database transaction that mutates state.

- When an aggregate raises a domain event, the event MUST be serialized
  and inserted into an `OutboxMessages` table within the same
  `SaveChangesAsync` transaction.
- A background dispatcher (`OutboxProcessor`) MUST poll or use
  change-tracking to pick up pending messages and publish them via
  MediatR (or a message broker) **after** the transaction commits.
- Outbox messages MUST be idempotent-safe: handlers MUST tolerate
  at-least-once delivery.
- The outbox table MUST track: `Id`, `OccurredOn`, `Type`,
  `Payload (JSON)`, `ProcessedOn`, `Error`.

**Rationale**: Decoupling event dispatch from the write transaction
prevents partial-commit failures where the DB write succeeds but the
event handler throws, leaving the system in an inconsistent state.

### VII. Test Coverage Targets

All pull requests MUST meet or exceed the following line-coverage
thresholds, enforced in CI:

| Layer            | Minimum Coverage |
|------------------|-----------------|
| Domain           | 90 %             |
| Application      | 85 %             |
| Infrastructure   | 70 %             |
| API              | 75 %             |

- **Domain tests** MUST be pure unit tests with zero infrastructure
  dependencies (no mocking of DbContext, no HTTP, no file I/O).
- **Application tests** MUST use in-memory fakes or mocks for
  repository interfaces; they MUST NOT require a running database.
- **Infrastructure tests** MUST use a real MSSQL instance (Testcontainers
  or LocalDB) for integration verification.
- **API tests** MUST use `WebApplicationFactory<Program>` for
  end-to-end endpoint validation.
- Coverage reports MUST be generated on every CI run and trend-tracked.

**Rationale**: Layer-specific targets ensure the highest-value code
(domain rules) receives the most rigorous testing, while pragmatically
accepting that infrastructure glue code is harder to cover exhaustively.

## Technology & Infrastructure Constraints

- **Runtime**: .NET 9 (latest LTS-track)
- **Database**: Microsoft SQL Server — single database per vendor
  deployment
- **ORM**: Entity Framework Core (latest stable) — code-first migrations
- **CQRS Mediator**: MediatR with pipeline behaviors (validation,
  logging, transaction, caching)
- **Caching**: `IMemoryCache` (default single-instance) with
  config-driven swap to `IDistributedCache` (Redis) for horizontal
  scaling
- **Authentication**: Internal JWT (HS256) + OAuth 2.0 (Google, Facebook)
  — provider selection via config
- **Payments**: Stripe, PayPal, Paymob — adapter activation via
  `vendor.config.json`
- **Shipping**: Flat-rate calculator + Shippo integration — adapter
  selection via config
- **Email**: SendGrid primary, SMTP fallback — config-driven swap
- **Real-Time**: SignalR hub for admin dashboard push notifications
- **Transactional Outbox**: EF Core–backed outbox table with background
  dispatcher

## Quality Gates & Development Workflow

- Every pull request MUST pass all CI checks before merge: build, lint,
  test coverage thresholds, JSON Schema validation of
  `vendor.config.json`.
- Configuration validation runs twice:
  1. **Boot-time**: FluentValidation rules execute at API startup;
     invalid config causes fail-fast.
  2. **CI/CD**: A standalone JSON Schema validator runs against the
     vendor config repository before deployment.
- Code review MUST verify compliance with all constitution principles.
  Reviewers MUST explicitly check:
  - Domain layer has zero external NuGet references.
  - No raw secrets in any committed file.
  - Handlers return `Result<T>`, not thrown exceptions.
  - Value objects use owned types, not separate tables.
  - Domain events use the outbox, not direct dispatch.
- Complexity MUST be justified. If a design introduces a pattern not
  mandated by this constitution, the PR description MUST include a
  rationale and confirmation that a simpler alternative was considered.

## Governance

This constitution is the highest-authority governance document for the
e-commerce platform project. It supersedes all other practice documents,
READMEs, and ad-hoc conventions.

- **Amendments** require: (1) a written proposal describing the change
  and its rationale, (2) review and approval by the project lead, and
  (3) a migration plan for any existing code that conflicts with the new
  rule.
- **Versioning** follows Semantic Versioning:
  - MAJOR: backward-incompatible governance changes (principle removal or
    redefinition).
  - MINOR: new principle or materially expanded guidance.
  - PATCH: clarifications, wording, typo fixes.
- **Compliance review**: every sprint retrospective MUST include a
  constitution compliance check. Violations discovered post-merge MUST
  be tracked as tech-debt items and resolved within the current or next
  sprint.
- Runtime development guidance is maintained in
  `docs/architecture-blueprint.md`.

**Version**: 1.0.0 | **Ratified**: 2026-07-25 | **Last Amended**: 2026-07-25
