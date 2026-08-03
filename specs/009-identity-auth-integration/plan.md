# Implementation Plan: Identity Auth Integration

**Branch**: `009-identity-auth-integration`
**Date**: 2026-07-29
**Spec**: [spec.md](./spec.md)

---

## Technical Context

- **Framework & Libraries**: .NET 9, ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`), Entity Framework Core 9.
- **Domain Layer**: `Customer` aggregate root in `Vendor.Domain` (zero NuGet references). Role and Status strictly owned by Customer.
- **Infrastructure Layer**: `ApplicationUser : IdentityUser<Guid>` with `CustomerId` FK, `VendorDbContext` EF Core mapping, `Google.Apis.Auth` ID token validation, Facebook Graph API token verification.
- **API Layer**: Existing `/api/v1/auth/*` Minimal API endpoints dispatching MediatR commands/queries.
- **Security & OAuth**: Google ID token server-side public key validation, Facebook Graph API `/me`, account takeover prevention for unverified emails, 5-attempt threshold with 15-minute lockout policy.

---

## Constitution Check

- [x] **Principle I: Clean Architecture**: `Vendor.Domain` has 0 external NuGet package references. `ApplicationUser` is isolated within `Vendor.Infrastructure.Identity`.
- [x] **Principle II: Result-Oriented Handlers**: All authentication handlers return `Result<T>` or `Result`.
- [x] **Principle III: MSSQL via EF Core**: Identity tables (`AspNetUsers`, `AspNetUserLogins`) and Customer 1:1 FK mapped in `VendorDbContext`.
- [x] **Principle IV: Clone-Per-Vendor**: Google Client ID and Facebook OAuth settings driven via configuration / secret references.
- [x] **Principle V: Secret Management**: External OAuth client secrets referenced via `ref:env:*`.
- [x] **Principle VII: Test Coverage**: Unit tests for handlers, integration tests for Identity DbContext and external token validation.

---

## Design Artifacts

- **Research Findings**: [research.md](./research.md)
- **Data Model**: [data-model.md](./data-model.md)
- **API Contracts**: [contracts/auth-endpoints.md](./contracts/auth-endpoints.md)
- **Quickstart Validation**: [quickstart.md](./quickstart.md)

---

## Implementation Phases

### Phase 0: Research & Setup
- Validate ASP.NET Core Identity EF Core setup and 1:1 `CustomerId` foreign key mapping.
- Document Google `GoogleJsonWebSignature` ID token validation and Facebook Graph API `/me` token verification.

### Phase 1: Data Model & Contracts
- Define `ApplicationUser` entity with `CustomerId` property.
- Configure `ApplicationUserConfiguration` in EF Core (`VendorDbContext`).
- Specify API contracts for `/auth/register`, `/auth/login`, `/auth/external/google`, and `/auth/external/facebook`.

### Phase 2: Foundational Identity Infrastructure
- Wire ASP.NET Core Identity in `DependencyInjection.cs` using `AddIdentityCore<ApplicationUser>()`.
- Configure `PasswordHasher`, `UserManager`, and `SignInManager` options (lockout: 5 attempts, 15 mins).

### Phase 3: Password Authentication & Registration Handlers
- Implement atomic registration transaction in `RegisterCommandHandler` creating `Customer` aggregate and `ApplicationUser` together.
- Update `LoginCommandHandler` to use `UserManager.CheckPasswordSignInAsync` with `lockoutOnFailure: true`.

### Phase 4: External OAuth Handlers (Google & Facebook)
- Implement `GoogleExternalAuthService` validating ID tokens against Google public keys.
- Implement `FacebookExternalAuthService` validating tokens via Graph API `/me`.
- Implement `ExternalLoginCommandHandler` enforcing verified email checks before `AddLoginAsync` or atomic creation.

### Phase 5: Verification & Lifecycle Token Operations
- Wire `VerifyEmailCommandHandler`, `ForgotPasswordCommandHandler`, and `ResetPasswordCommandHandler` to `UserManager` token services.

### Phase 6: Polish & Database Migration
- Generate EF Core migration `AddIdentityAuthIntegration`.
- Run full solution test suite ensuring coverage targets are met.
