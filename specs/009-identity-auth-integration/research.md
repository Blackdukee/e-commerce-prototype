# Research Findings: Identity Auth Integration

**Feature**: `009-identity-auth-integration`
**Date**: 2026-07-29

## 1. ASP.NET Core Identity Integration Architecture

### Decision
Use `Microsoft.AspNetCore.Identity.EntityFrameworkCore` inside `Vendor.Infrastructure` to configure `ApplicationUser : IdentityUser<Guid>`.

### Rationale
- `ApplicationUser` inherits from `IdentityUser<Guid>` to provide native ASP.NET Core `UserManager<ApplicationUser>` support for password hashing (`IPasswordHasher<ApplicationUser>`), email token generation, lockout counters, and external login linkage (`AspNetUserLogins`).
- Domain's `Customer` aggregate root remains strictly clean in `Vendor.Domain` without referencing any ASP.NET Core Identity or EF Core packages (complying with Constitution Rule I).
- Role and Status stay strictly owned by `Customer` aggregate (`CustomerRole`, `CustomerStatus`); `IdentityRole` tables are not registered or populated.

### Alternatives Considered
- *Custom Identity Store from scratch*: High complexity and maintenance burden without leverage of standard `UserManager` security behaviors (e.g. security stamp validation, lockout handling, token generation).
- *Duplicating Roles into Identity*: Risk of data desynchronization between `Customer.Role` and Identity role tables. Rejected per explicit spec directive.

---

## 2. One-to-One Atomic Registration & Customer Transaction Pattern

### Decision
Wrap registration (`POST /auth/register`) and first-time external OAuth sign-in (`POST /auth/external/*`) within an EF Core execution strategy transaction (`IDbContextTransaction`) using `VendorDbContext`.

### Rationale
- Guarantees that `ApplicationUser` and paired `Customer` aggregate are created atomically in a single database transaction.
- If Customer creation or Identity `UserManager.CreateAsync` fails, the transaction rolls back completely, preventing orphaned Identity or Customer records.

### Implementation Pattern
```csharp
await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
try
{
    var customer = new Customer(name, email, phone);
    await customerRepository.AddAsync(customer, ct);
    await dbContext.SaveChangesAsync(ct);

    var user = new ApplicationUser
    {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        CustomerId = customer.Id.Value
    };

    var result = await userManager.CreateAsync(user, password);
    if (!result.Succeeded)
    {
        await transaction.RollbackAsync(ct);
        return Result.Failure(MapIdentityError(result.Errors));
    }

    await transaction.CommitAsync(ct);
    return Result.Success(user);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

---

## 3. Google ID Token Server-Side Public Key Validation

### Decision
Use `Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync` server-side to validate Google ID tokens against Google's public key endpoint (`https://www.googleapis.com/oauth2/3/certs`).

### Rationale
- Verifies token signature cryptographic integrity using Google's public keys.
- Enforces audience matching against the configured Google OAuth Client ID (`GoogleJsonWebSignature.ValidationSettings { Audience = [googleClientId] }`).
- Checks payload expiration (`exp`) and extracts verified email status (`Payload.EmailVerified`) and Google subject claim (`Payload.Subject`).

### Account Takeover Prevention Flow
```csharp
var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);
var googleUserKey = payload.Subject;
var email = payload.Email;
var isEmailVerified = payload.EmailVerified;

var user = await userManager.FindByLoginAsync("Google", googleUserKey);
if (user is null)
{
    var existingUser = await userManager.FindByEmailAsync(email);
    if (existingUser is not null)
    {
        if (!isEmailVerified)
        {
            return Result.Failure("Auth.UnverifiedEmailConflict", "Email is not verified by Google. Please sign in with password first.");
        }
        await userManager.AddLoginAsync(existingUser, new UserLoginInfo("Google", googleUserKey, "Google"));
        user = existingUser;
    }
    else
    {
        // Atomic creation of Customer + ApplicationUser, then AddLoginAsync
    }
}
```

---

## 4. Facebook Graph API Server-Side Token Verification

### Decision
Validate Facebook access tokens server-side by making an HTTP call to Facebook Graph API `https://graph.facebook.com/v19.0/me?fields=id,name,email&access_token={token}` using `IHttpClientFactory`.

### Rationale
- Validates that the access token belongs to a legitimate Facebook user session.
- Returns Facebook user ID (`id`), name, and email.
- Follows the exact parallel logic as Google authentication (checking `FindByLoginAsync("Facebook", facebookUserId)`, email matching, and verified email checks).

---

## 5. JWT Issuance & Identity Integration

### Decision
Maintain existing `JwtTokenService` generating HS256 JWT access and refresh token pairs after `UserManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)` or external login succeeds.

### Rationale
- Identity handles credential checking, password hashing, lockout state, and token generation for reset/confirmation.
- Token issuance remains stateless JWT bearer tokens for SPA/BFF clients (no cookie authentication).
- Propagates `email_verified` claim based on `user.EmailConfirmed`.
