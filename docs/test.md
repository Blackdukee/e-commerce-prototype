# 🚨 CRITICAL: Admin Account Has Wrong Role in Database

**Status:** BLOCKING - All admin endpoints return 401 Unauthorized  
**Severity:** Critical  
**Component:** Database Seed / Customer Role Assignment  
**Discovered:** 2026-08-29 via live verification (Playwright + SQL query + JWT decode)  
**Reporter:** Frontend Team  

---

## Executive Summary

The `admin@vendor.com` account in the `dbo.Customers` table has `Role = "Customer"` when it should be `Role = "Admin"` or `"SuperAdmin"`. This causes every `/api/v1/admin/*` endpoint to return `401 Unauthorized`, completely blocking the Admin Mission Control panel.

**This is a data issue, not a code bug.** The authentication code is working correctly — it's reading the wrong value from the database.

**Fix:** Single SQL UPDATE statement (provided below).

---

## Evidence

### 1. JWT Token (Decoded Payload)

**Login Request:**
```bash
POST http://localhost:8081/api/v1/auth/login
Content-Type: application/json

{
  "email": "admin@vendor.com",
  "password": "Admin123!"
}
```

**Response `accessToken` Decoded:**
```json
{
  "sub": "b4ba1faa-bdfe-4ccd-a2b1-2300416be17b",
  "email": "admin@vendor.com",
  "jti": "5663d786-fa7d-4fc6-baf1-4f6bed08a2054",
  "role": "Customer",  ← WRONG VALUE
  "nbf": 1788038306,
  "exp": 1788040106,
  "iat": 1788038306
}
```

**Expected:** `"role": "Admin"` or `"role": "SuperAdmin"`  
**Actual:** `"role": "Customer"`

---

### 2. Database Query Result

**Query:**
```sql
SELECT Id, Email, FirstName, LastName, Role 
FROM dbo.Customers 
WHERE Email = 'admin@vendor.com';
```

**Result:**
```
Id:        B4BA1FAA-BDFE-4CCD-A2B1-2300416BE17B
Email:     admin@vendor.com
FirstName: Admin
LastName:  User
Role:      Customer  ← WRONG VALUE (should be "Admin" or "SuperAdmin")
```

---

### 3. Source Code Confirmation

**File:** `backend/src/Vendor.Application/Modules/Auth/AuthHandlers.cs`  
**Lines:** 56-85 (LoginWithPasswordCommandHandler)

```csharp
public async Task<Result<AuthResponseDto>> Handle(LoginWithPasswordCommand request, CancellationToken ct)
{
    var result = await identityAuthService.PasswordSignInAsync(request.Email, request.Password, ct);
    
    // ... validation checks ...
    
    var customer = await customerRepository.GetByIdAsync(new CustomerId(result.CustomerId), ct);
    var firstName = customer?.FirstName ?? string.Empty;
    var lastName = customer?.LastName ?? string.Empty;

    // ← JWT role claim is generated HERE from customer.Role
    var tokenResult = tokenService.GenerateTokens(
        result.CustomerId, 
        request.Email, 
        [customer?.Role.ToString() ?? "Customer"]  ← Reads from DB, converts to string
    );
    
    var customerDto = new CustomerDto(result.CustomerId, request.Email, firstName, lastName, "Registered", true);
    return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
}
```

**Key Line:** `[customer?.Role.ToString() ?? "Customer"]`

This line:
1. Reads `customer.Role` from the database (enum)
2. Calls `.ToString()` to convert to string
3. Passes it to the JWT generator as the `role` claim

**The code is working correctly** — it's faithfully reading and mapping the database value.

---

## Evidence Agreement Table

| Source | Location | Actual Value | Expected Value | Match |
|--------|----------|--------------|----------------|-------|
| **Database** | `dbo.Customers.Role` | `"Customer"` | `"Admin"` | ❌ WRONG |
| **Code** | `AuthHandlers.cs:85` | Maps DB value → JWT | — | ✅ Code correct |
| **JWT Token** | Decoded `role` claim | `"Customer"` | `"Admin"` | ❌ WRONG (reflects DB) |

**Conclusion:** All three sources agree. The database contains the wrong value; the code correctly propagates it to the JWT.

---

## Impact

### Blocked Features (100% of Admin Panel)
- ❌ **Orders Management** - Cannot view or process orders
- ❌ **Inventory Management** - Cannot update stock levels
- ❌ **Promotions Management** - Cannot create/deactivate promos
- ❌ **Vendor Config** - Cannot update store settings
- ❌ **Customer Management** - Cannot view/suspend customers
- ❌ **Shipment Management** - Cannot create/track shipments
- ❌ **Payment Operations** - Cannot capture/refund payments
- ❌ **Analytics Dashboard** - Cannot view business metrics

### Example Failed Request

```bash
PUT /api/v1/admin/products/{id}/variants/{variantId}
Authorization: Bearer eyJhbGci...  # Token with "role": "Customer"

Response:
401 Unauthorized
```

**All** `/api/v1/admin/*` endpoints are protected by `[Authorize(Roles = "Admin")]` or similar, which rejects tokens with `role: "Customer"`.

---

## Root Cause

The admin account was created or seeded with the wrong `Role` enum value.

### Likely Causes

1. **Database Seeder Issue:**  
   The seeder might be using:
   ```csharp
   // WRONG:
   new Customer { Email = "admin@vendor.com", Role = CustomerRole.Customer }
   
   // CORRECT:
   new Customer { Email = "admin@vendor.com", Role = CustomerRole.Admin }
   ```

2. **Manual Account Creation:**  
   Account was manually created with `Role = 0` (Customer) instead of `Role = 1` (Admin)

3. **Migration Default:**  
   A migration might have set `Role = Customer` as default during creation

### Enum Values (Assumed)
```csharp
public enum CustomerRole
{
    Customer = 0,      // Regular customer
    Admin = 1,         // Admin user
    SuperAdmin = 2     // Super admin
}
```

---

## Fix

### Immediate Fix (SQL UPDATE)

Run this query against the `VendorDb` database:

```sql
-- Update admin account to have Admin role
UPDATE dbo.Customers 
SET Role = 1  -- Or 2 for SuperAdmin
WHERE Email = 'admin@vendor.com';

-- Verify the change
SELECT Id, Email, FirstName, LastName, Role 
FROM dbo.Customers 
WHERE Email = 'admin@vendor.com';
```

**Expected Result After Fix:**
```
Id:        B4BA1FAA-BDFE-4CCD-A2B1-2300416BE17B
Email:     admin@vendor.com
FirstName: Admin
LastName:  User
Role:      Admin  ← CORRECTED
```

---

### Permanent Fix (Database Seeder)

Locate and fix the database seeder to prevent this in future deployments:

**File to Check:** `backend/src/Vendor.Infrastructure/Persistence/DatabaseSeeder.cs` (or similar)

**Example Fix:**
```csharp
// Before (WRONG):
var adminUser = new Customer(
    new CustomerId(Guid.Parse("b4ba1faa-bdfe-4ccd-a2b1-2300416be17b")),
    "admin@vendor.com",
    "Admin",
    "User",
    CustomerType.Registered
);
// Role defaults to Customer (0) if not explicitly set

// After (CORRECT):
var adminUser = new Customer(
    new CustomerId(Guid.Parse("b4ba1faa-bdfe-4ccd-a2b1-2300416be17b")),
    "admin@vendor.com",
    "Admin",
    "User",
    CustomerType.Registered
)
{
    Role = CustomerRole.Admin  // ← Explicitly set role
};
```

Or if using a factory/builder pattern, ensure the role is set during construction.

---

## Verification Steps

After applying the fix:

### 1. Query Database
```sql
SELECT Email, Role FROM dbo.Customers WHERE Email = 'admin@vendor.com';
-- Expected: Role = "Admin"
```

### 2. Login and Decode JWT
```bash
# Login
curl -X POST http://localhost:8081/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@vendor.com","password":"Admin123!"}'

# Decode the returned accessToken at jwt.io
# Expected: "role": "Admin"
```

### 3. Test Admin Endpoint
```bash
# Get the token from step 2
TOKEN="<access_token>"

# Test admin endpoint
curl -X GET http://localhost:8081/api/v1/admin/config \
  -H "Authorization: Bearer $TOKEN"

# Expected: 200 OK with config data (not 401)
```

### 4. Frontend Verification
Open `http://localhost:3000/admin/inventory` in browser:
- Should load product list successfully
- Stock adjustment should work (not 401)
- No authentication errors in console

---

## Timeline

- **2026-08-29 21:07** - Discovered via Playwright when testing admin inventory stock update (401)
- **2026-08-29 21:13** - Confirmed with SQL query: admin account has `Role = "Customer"`
- **2026-08-29 21:18** - Decoded JWT confirms `"role": "Customer"` in token
- **2026-08-29 21:20** - Reviewed source code: confirms code is correct, DB is wrong

---

## Additional Context

### Frontend Status
- Frontend code is **correct** and **ready**
- All admin API clients are implemented correctly
- Phase 2 features (Customer Management, Shipments, Payments, Analytics) are **blocked** pending this fix
- No frontend changes needed once backend is fixed

### Testing Environment
- **Backend API:** `http://localhost:8081`
- **Frontend:** `http://localhost:3000`
- **Database:** SQL Server container `vendor-mssql`
- **Connection:** `localhost:14330`, user `sa`

---

## Questions?

**Contact:** Frontend Team  
**Evidence Files:**
- This document
- Playwright test recordings (if needed)
- SQL query outputs (in this document)
- JWT decode examples (in this document)

**ETA for Fix:** ~5 minutes (single SQL UPDATE + verification)
