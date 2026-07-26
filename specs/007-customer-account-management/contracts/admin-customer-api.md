# API Contract: Admin Customer Management Endpoints

**Feature**: 007-customer-account-management
**Base Route**: `/api/v1/admin/customers`
**Authentication**: Required (`Authorization: Bearer <JWT>`)

---

## 1. List Customers (Paginated & Filterable)

- **HTTP Method**: `GET`
- **Path**: `/api/v1/admin/customers`
- **Authorization**: `Admin` or `SuperAdmin` role
- **Query Parameters**:
  - `email` (string, optional) — Case-insensitive search filter
  - `role` (string, optional) — Filter by role: `Customer`, `Admin`, `SuperAdmin`
  - `status` (string, optional) — Filter by status: `Active`, `Suspended`
  - `registeredFrom` (ISO 8601 string, optional) — Filter registration date start
  - `registeredTo` (ISO 8601 string, optional) — Filter registration date end
  - `page` (int, default = 1)
  - `pageSize` (int, default = 20, max = 100)

### Response (`200 OK`):
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "john.doe@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "customerType": "Registered",
      "role": "Customer",
      "status": "Active",
      "createdAtUtc": "2026-01-15T10:30:00Z",
      "suspendedAtUtc": null,
      "suspensionReason": null
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

---

## 2. Get Customer Detail & Order History

- **HTTP Method**: `GET`
- **Path**: `/api/v1/admin/customers/{id}`
- **Authorization**: `Admin` or `SuperAdmin` role

### Response (`200 OK`):
```json
{
  "profile": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "customerType": "Registered",
    "role": "Customer",
    "status": "Active",
    "createdAtUtc": "2026-01-15T10:30:00Z",
    "suspendedAtUtc": null,
    "suspensionReason": null,
    "roleChangedAtUtc": null,
    "roleChangedByCustomerId": null
  },
  "orderHistory": [
    {
      "orderId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "orderNumber": "ACM-10042",
      "status": "Delivered",
      "totalAmount": 149.99,
      "currency": "USD",
      "createdAtUtc": "2026-02-01T14:20:00Z"
    }
  ]
}
```

### Error Responses:
- `404 Not Found`: Customer does not exist.

---

## 3. Suspend Customer Account

- **HTTP Method**: `POST`
- **Path**: `/api/v1/admin/customers/{id}/suspend`
- **Authorization**: `Admin` or `SuperAdmin` role

### Request Body:
```json
{
  "reason": "Suspicious login attempts and fraudulent chargeback"
}
```

### Response (`200 OK`):
```json
{
  "message": "Customer account suspended successfully."
}
```

### Error Responses:
- `400 Bad Request`: SuperAdmin attempting to suspend their own account (`SelfModificationNotAllowed`).
- `404 Not Found`: Customer does not exist.

---

## 4. Reactivate Customer Account

- **HTTP Method**: `POST`
- **Path**: `/api/v1/admin/customers/{id}/reactivate`
- **Authorization**: `Admin` or `SuperAdmin` role

### Response (`200 OK`):
```json
{
  "message": "Customer account reactivated successfully."
}
```

---

## 5. Promote Customer to Admin

- **HTTP Method**: `POST`
- **Path**: `/api/v1/admin/customers/{id}/promote`
- **Authorization**: Restricted to `SuperAdmin` role ONLY (Enforced at API & Command Handler level)
- **Rate Limit Policy**: `"auth"` policy (10 req/min)

### Response (`200 OK`):
```json
{
  "message": "Customer successfully promoted to Admin role."
}
```

### Error Responses:
- `403 Forbidden`: Caller is not a `SuperAdmin`.
- `400 Bad Request`: Target account is already Admin or is SuperAdmin.
- `429 Too Many Requests`: Exceeded auth rate limit.

---

## 6. Demote Admin to Customer

- **HTTP Method**: `POST`
- **Path**: `/api/v1/admin/customers/{id}/demote`
- **Authorization**: Restricted to `SuperAdmin` role ONLY (Enforced at API & Command Handler level)
- **Rate Limit Policy**: `"auth"` policy (10 req/min)

### Response (`200 OK`):
```json
{
  "message": "Admin successfully demoted to Customer role."
}
```

### Error Responses:
- `403 Forbidden`: Caller is not a `SuperAdmin`.
- `400 Bad Request`: Caller attempting self-demotion or target account is SuperAdmin.
- `429 Too Many Requests`: Exceeded auth rate limit.

---

## 7. Get Customer Account Audit Log

- **HTTP Method**: `GET`
- **Path**: `/api/v1/admin/customers/{id}/audit-log`
- **Authorization**: Restricted to `SuperAdmin` role ONLY
- **Query Parameters**:
  - `page` (int, default = 1)
  - `pageSize` (int, default = 20)

### Response (`200 OK`):
```json
{
  "items": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "eventType": "Suspended",
      "details": {
        "reason": "Fraudulent activity",
        "suspendedBy": "98765432-1234-5678-90ab-cdef12345678"
      },
      "performedByCustomerId": "98765432-1234-5678-90ab-cdef12345678",
      "timestampUtc": "2026-03-01T12:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```
