# Feature Specification: Identity Auth Integration

**Feature Branch**: `009-identity-auth-integration`

**Created**: 2026-07-29

**Status**: Draft

**Input**: User description: "Wire Vendor.Infrastructure's auth implementation to ASP.NET Core Identity. Add an ApplicationUser identity type (email, password hash, lockout state, external login records) linked one-to-one to the Domain's Customer aggregate via a CustomerId foreign key — Identity owns credentials and external-login linkage only; Role and Status stay owned by the Customer aggregate, never duplicated into Identity's role tables. Registration and first-time external login both create the ApplicationUser and its paired Customer aggregate in a single transaction; the two are never created independently. Google login: the frontend obtains a Google ID token client-side and posts it to POST /auth/external/google. The handler validates the token server-side against Google's public keys with our OAuth client ID as the expected audience, then looks up the login via UserManager.FindByLoginAsync using "Google" as the provider and the token's subject claim as the provider key. If no linked login exists yet, look up by email: if an account with that email exists and Google reports the email as verified, link the Google login to that existing account via AddLoginAsync; if the email exists but is not verified by Google, fail with a distinct conflict error instructing the user to sign in with their password first rather than silently linking (this prevents account takeover via an unverified email claim); if no account exists at all, create a new ApplicationUser and Customer (role Customer, status Active) together, then link the Google login. Facebook follows the same shape using the Graph API /me endpoint and "Facebook" as the login provider — implement it as a parallel path, not a special case. Login and registration continue to issue the existing JWT access/refresh token pair from JwtTokenService after Identity confirms the credentials — Identity is not used for cookie-based sign-in, since this is an API consumed by a separate SPA/BFF frontend. Use UserManager.CheckPasswordSignInAsync with lockoutOnFailure enabled for password login, UserManager.GenerateEmailConfirmationTokenAsync / ConfirmEmailAsync for the existing verify-email endpoint, and GeneratePasswordResetTokenAsync / ResetPasswordAsync for the existing forgot/reset-password endpoints. No new routes — this only changes what Vendor.Infrastructure does behind the existing /auth/* endpoints from Phase E."

## Clarifications

### Session 2026-07-29

- Q: What failed attempt threshold and lockout duration should ASP.NET Core Identity enforce upon repeated password failures? → A: 5 failed attempts trigger a 15-minute lockout period.
- Q: Should password sign-in strictly require an email address to be confirmed before issuing JWT token pairs? → A: Allow login for unconfirmed emails, propagating `email_verified` claim in JWT.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure Identity Password Authentication & Registration (Priority: P1)

Users can register for a new account or sign in with their password using ASP.NET Core Identity credential checking, receiving JWT access/refresh token pairs, while lockout enforcement prevents brute-force attacks.

**Why this priority**: Password registration and sign-in are the core authentication mechanisms required for all system access.

**Independent Test**: Register a new user via `POST /auth/register`, verify that an identity account and paired Customer aggregate are created together in a single transaction, and verify password login via `POST /auth/login` issues valid JWT tokens while locking out after 5 consecutive failed attempts.

**Acceptance Scenarios**:

1. **Given** a new email and password, **When** submitting `POST /auth/register`, **Then** both an identity record and paired Customer aggregate are atomically created, and a JWT access/refresh token pair is returned.
2. **Given** registered credentials, **When** submitting valid credentials to `POST /auth/login`, **Then** password sign-in check succeeds and a JWT token pair with `email_verified` claim is returned.
3. **Given** registered credentials, **When** submitting 5 consecutive invalid password attempts to `POST /auth/login`, **Then** the account is locked out for 15 minutes and subsequent login attempts fail with lockout status.

---

### User Story 2 - Google & Facebook External Provider OAuth Integration (Priority: P2)

Users can sign in or register seamlessly using Google or Facebook OAuth tokens, automatically linking external logins to existing or new Customer accounts based on email verification.

**Why this priority**: Social logins reduce registration friction and provide modern passwordless entry for buyers.

**Independent Test**: Post a valid Google ID token to `POST /auth/external/google`. Verify that first-time login creates both identity and Customer aggregate and links the Google login provider, while subsequent logins reuse the linked login to issue JWT tokens.

**Acceptance Scenarios**:

1. **Given** a valid Google ID token for an unregistered email, **When** posting to `POST /auth/external/google`, **Then** a new identity user and paired Customer aggregate (Role: Customer, Status: Active) are created atomically and linked to the Google provider key.
2. **Given** a valid Google ID token for an existing email marked as verified by Google, **When** posting to `POST /auth/external/google`, **Then** the Google provider key is linked to the existing account and a JWT token pair is returned.
3. **Given** a valid Google ID token for an existing email marked as UNVERIFIED by Google, **When** posting to `POST /auth/external/google`, **Then** authentication fails with a 409 Conflict error instructing the user to sign in with their password first.
4. **Given** a valid Facebook access token, **When** posting to `POST /auth/external/facebook`, **Then** Facebook user details are validated via Graph API `/me` following the parallel external login workflow.

---

### User Story 3 - Identity Lifecycle Email Verification & Password Reset (Priority: P3)

Users can verify their email address or reset forgotten passwords via secure token-based workflows powered by ASP.NET Core Identity.

**Why this priority**: Self-service email verification and password recovery are required lifecycle features for user maintenance.

**Independent Test**: Request password reset via `POST /auth/forgot-password`, obtain the identity reset token, and reset the password via `POST /auth/reset-password`, confirming the new password allows login.

**Acceptance Scenarios**:

1. **Given** a registered email, **When** requesting email verification token, **Then** an identity email confirmation token is generated and can be verified via `POST /auth/verify-email`.
2. **Given** a registered email, **When** requesting password reset, **Then** an identity password reset token is generated and resetting password via `POST /auth/reset-password` updates the credentials.

---

### Edge Cases

- What happens if database transaction fails after creating Customer aggregate but before committing Identity user? The transaction MUST roll back entirely so that ApplicationUser and Customer aggregate are never left orphaned.
- How does the system handle an external login attempt with a tampered or expired OAuth token? Server-side public key validation fails and returns an HTTP 401 Unauthorized response without creating any identity or domain records.
- How does the system handle user roles? Role and status remain strictly stored within the Customer domain aggregate; Identity role tables are not used.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST create an identity record linked one-to-one to the Customer domain aggregate via a `CustomerId` reference.
- **FR-002**: Identity MUST own password hashes, email confirmation tokens, lockout state, and external login linkages only; Customer roles and statuses MUST remain strictly owned by the Customer aggregate.
- **FR-003**: System MUST create identity user records and paired Customer domain aggregates within a single atomic database transaction.
- **FR-004**: System MUST validate Google ID tokens server-side against Google's public key endpoint using the configured OAuth Client ID as expected audience.
- **FR-005**: System MUST validate Facebook access tokens server-side using the Facebook Graph API `/me` endpoint.
- **FR-006**: When authenticating external logins for existing email addresses, system MUST require the external provider to report the email address as verified before linking the login via `AddLoginAsync`.
- **FR-007**: If an external provider reports an unverified email address for an existing account, system MUST reject authentication with an HTTP 409 Conflict error instructing password login first.
- **FR-008**: System MUST execute password login credential validation using identity password sign-in checking (`CheckPasswordSignInAsync`) enforcing a 5-failed-attempt threshold with a 15-minute lockout period.
- **FR-009**: System MUST generate JWT access and refresh token pairs via `JwtTokenService` upon successful identity authentication (propagating `email_verified` claim for unconfirmed email accounts) without using cookie-based sign-in.
- **FR-010**: System MUST execute email confirmation and password reset workflows using identity token generation and validation services.
- **FR-011**: All authentication operations MUST maintain exact existing REST route paths under `/auth/*`.

### Key Entities

- **ApplicationUser**: Identity user representation holding credential hashes, email confirmation flags, lockout flags, external login bindings, and a `CustomerId` foreign key to the Customer domain aggregate.
- **Customer**: Domain aggregate root holding customer profile data, role (`Customer`, `Admin`), and account status (`Active`, `Suspended`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Password authentication and external OAuth login complete and issue JWT token pairs in under 500 milliseconds.
- **SC-002**: 100% of user registrations and first-time social logins create both identity user and Customer domain aggregate atomically without orphan records.
- **SC-003**: Account takeover attempts via unverified third-party emails are blocked 100% of the time with explicit 409 Conflict responses.
- **SC-004**: 5 consecutive invalid password login attempts reliably trigger a 15-minute account lockout state.

## Assumptions

- Frontend applications interact exclusively with the stateless REST API and send JWT bearer tokens for authorized requests.
- External OAuth provider public keys and client configurations are supplied via environment variable configuration.
- Existing `/auth/*` API route signatures and response schemas remain unchanged.
