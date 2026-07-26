# Data Model: Customer Account Management

**Feature**: 007-customer-account-management
**Date**: 2026-07-26

## Domain Layer Models (`Vendor.Domain`)

### Enums

#### `CustomerRole`
```csharp
namespace Vendor.Domain.Aggregates.Customer;

public enum CustomerRole
{
    Customer = 0,
    Admin = 1,
    SuperAdmin = 2
}
```

#### `CustomerStatus`
```csharp
namespace Vendor.Domain.Aggregates.Customer;

public enum CustomerStatus
{
    Active = 0,
    Suspended = 1
}
```

### Aggregate Root: `Customer` (Extended)

| Property | Type | Constraints / Default | Description |
|---|---|---|---|
| `Id` | `CustomerId` | PK, Guid wrapper | Unique customer identity |
| `Email` | `string` | Required, unique, lowercased | Customer email address |
| `FirstName` | `string` | Required, max 100 | First name |
| `LastName` | `string` | Required, max 100 | Last name |
| `CustomerType` | `CustomerType` | Guest / Registered | Customer registration tier |
| `Role` | `CustomerRole` | Default: `CustomerRole.Customer` | Access level (Customer, Admin, SuperAdmin) |
| `Status` | `CustomerStatus` | Default: `CustomerStatus.Active` | Account status (Active, Suspended) |
| `SuspendedAtUtc` | `DateTime?` | Nullable | Timestamp when account was suspended |
| `SuspensionReason` | `string?` | Nullable, max 500 | Reason specified for account suspension |
| `RoleChangedAtUtc` | `DateTime?` | Nullable | Timestamp of last role promotion/demotion |
| `RoleChangedByCustomerId` | `CustomerId?` | Nullable | ID of SuperAdmin who changed the role |
| `CreatedAtUtc` | `DateTime` | Set on creation | Account creation timestamp |

### Domain Events

1. **`CustomerSuspendedEvent`**:
   - `CustomerId`: `CustomerId`
   - `Reason`: `string`
   - `SuspendedAtUtc`: `DateTime`
   - `SuspendedBy`: `CustomerId`
2. **`CustomerReactivatedEvent`**:
   - `CustomerId`: `CustomerId`
   - `ReactivatedAtUtc`: `DateTime`
   - `ReactivatedBy`: `CustomerId`
3. **`CustomerRoleChangedEvent`**:
   - `CustomerId`: `CustomerId`
   - `PreviousRole`: `CustomerRole`
   - `NewRole`: `CustomerRole`
   - `ChangedBy`: `CustomerId`
   - `ChangedAtUtc`: `DateTime`

---

## Infrastructure Layer Models (`Vendor.Infrastructure`)

### Table Schema Updates: `Customers` Table

EF Core mapping in `CustomerConfiguration.cs`:

```csharp
builder.Property(c => c.Role)
    .HasConversion<string>()
    .HasMaxLength(20)
    .IsRequired()
    .HasDefaultValue(CustomerRole.Customer);

builder.Property(c => c.Status)
    .HasConversion<string>()
    .HasMaxLength(20)
    .IsRequired()
    .HasDefaultValue(CustomerStatus.Active);

builder.Property(c => c.SuspendedAtUtc).IsRequired(false);
builder.Property(c => c.SuspensionReason).HasMaxLength(500).IsRequired(false);
builder.Property(c => c.RoleChangedAtUtc).IsRequired(false);
builder.Property(c => c.RoleChangedByCustomerId)
    .HasConversion(id => id != null ? id.Value : (Guid?)null, value => value.HasValue ? new CustomerId(value.Value) : null)
    .IsRequired(false);
```

### Table Schema: `CustomerAuditLogs` Table

```csharp
public class CustomerAuditLog
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string EventType { get; set; } = null!; // "Suspended", "Reactivated", "RoleChanged"
    public string DetailsJson { get; set; } = null!;
    public Guid PerformedByCustomerId { get; set; }
    public DateTime TimestampUtc { get; set; }
}
```

---

## Repository Extension: `ICustomerRepository`

```csharp
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
    
    // New Extension Methods for Management & Audit
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        string? emailSearch,
        CustomerRole? role,
        CustomerStatus? status,
        DateTime? registeredFrom,
        DateTime? registeredTo,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);

    Task<(IReadOnlyList<CustomerAuditLog> Items, int TotalCount)> GetAuditLogsAsync(
        CustomerId customerId,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default);
}
```
