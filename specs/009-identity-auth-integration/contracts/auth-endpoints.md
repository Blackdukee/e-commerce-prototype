# Endpoint Contracts: Identity Auth Integration

**Feature**: `009-identity-auth-integration`
**Date**: 2026-07-29

All authentication endpoints maintain existing REST route structures under `/api/v1/auth/`.

---

## 1. Registration (`POST /api/v1/auth/register`)

Registers a new user and Customer aggregate in a single atomic transaction.

### Request Body
```json
{
  "email": "buyer@example.com",
  "password": "SecurePassword123!",
  "fullName": "Jane Doe",
  "phoneNumber": "+15551234567"
}
```

### Response (`201 Created`)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1Ni...",
  "refreshToken": "d7a8b9c0...",
  "expiresAtUtc": "2026-07-29T16:25:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "customerId": "8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c3d",
    "email": "buyer@example.com",
    "fullName": "Jane Doe",
    "role": "Customer",
    "emailConfirmed": false
  }
}
```

---

## 2. Password Login (`POST /api/v1/auth/login`)

Validates credentials via Identity `UserManager.CheckPasswordSignInAsync` with `lockoutOnFailure: true`.

### Request Body
```json
{
  "email": "buyer@example.com",
  "password": "SecurePassword123!"
}
```

### Response (`200 OK`)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1Ni...",
  "refreshToken": "d7a8b9c0...",
  "expiresAtUtc": "2026-07-29T16:25:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "customerId": "8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c3d",
    "email": "buyer@example.com",
    "fullName": "Jane Doe",
    "role": "Customer",
    "emailConfirmed": false
  }
}
```

### Error Responses
- `400 Bad Request` (Invalid credentials)
- `423 Locked Out` (Account locked due to 5 consecutive failed attempts)

---

## 3. External Google Login (`POST /api/v1/auth/external/google`)

Validates client Google ID token server-side and issues JWT tokens.

### Request Body
```json
{
  "idToken": "eyJhbGciOiJSUzI1Ni..."
}
```

### Response (`200 OK`)
```json
{
  "accessToken": "eyJhbGciOiJIUzI1Ni...",
  "refreshToken": "d7a8b9c0...",
  "expiresAtUtc": "2026-07-29T16:25:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "customerId": "8a7b6c5d-4e3f-2a1b-0c9d-8e7f6a5b4c3d",
    "email": "buyer@gmail.com",
    "fullName": "Google User",
    "role": "Customer",
    "emailConfirmed": true
  }
}
```

### Error Response (`409 Conflict`)
Occurs when email exists but is reported as unverified by Google:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Unverified Email Conflict",
  "status": 409,
  "detail": "An account with this email address already exists. Please sign in with your password first to link Google login."
}
```

---

## 4. External Facebook Login (`POST /api/v1/auth/external/facebook`)

Validates Facebook access token server-side via Graph API `/me`.

### Request Body
```json
{
  "accessToken": "EAABwz..."
}
```

### Response (`200 OK`)
Same token response payload as Google login.
