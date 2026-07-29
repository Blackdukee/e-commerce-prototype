using Microsoft.AspNetCore.Identity;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; } = CustomerType.Registered;
    public CustomerRole Role { get; set; } = CustomerRole.Customer;
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    public bool AnalyticsConsent { get; set; }
    public DateTime? ConsentUpdatedAtUtc { get; set; }
    public DateTime? RegisteredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SuspendedAtUtc { get; set; }
    public string? SuspensionReason { get; set; }

    public Customer ToDomainEntity()
    {
        return new Customer(
            new CustomerId(Id),
            Email ?? UserName ?? string.Empty,
            FirstName,
            LastName,
            CustomerType,
            AnalyticsConsent,
            Role,
            Status);
    }
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
