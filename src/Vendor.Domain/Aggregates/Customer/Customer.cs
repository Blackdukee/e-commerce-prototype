using Vendor.Domain.Abstractions;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Customer;

public enum CustomerType
{
    Guest,
    Registered
}

public class Customer : AggregateRoot<CustomerId>
{
    private readonly List<Address> _shippingAddresses = [];

    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public CustomerType CustomerType { get; private set; }
    public CustomerRole Role { get; private set; }
    public CustomerStatus Status { get; private set; }
    public DateTime? SuspendedAtUtc { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTime? RoleChangedAtUtc { get; private set; }
    public CustomerId? RoleChangedByCustomerId { get; private set; }
    public bool AnalyticsConsent { get; private set; }
    public DateTime? ConsentUpdatedAtUtc { get; private set; }
    public DateTime? RegisteredAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Address> ShippingAddresses => _shippingAddresses.AsReadOnly();

    private Customer() : base(default!)
    {
        Email = null!;
        FirstName = null!;
        LastName = null!;
    }

    public Customer(
        CustomerId id,
        string email,
        string firstName,
        string lastName,
        CustomerType customerType = CustomerType.Guest,
        bool analyticsConsent = false,
        CustomerRole role = CustomerRole.Customer,
        CustomerStatus status = CustomerStatus.Active) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName, nameof(firstName));
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName, nameof(lastName));

        Email = email.Trim().ToLowerInvariant();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        CustomerType = customerType;
        Role = role;
        Status = status;
        AnalyticsConsent = analyticsConsent;
        CreatedAtUtc = DateTime.UtcNow;

        if (customerType == CustomerType.Registered)
        {
            RegisteredAtUtc = CreatedAtUtc;
        }

        RaiseDomainEvent(new CustomerCreatedEvent(Id, Email, CreatedAtUtc));
    }

    public void RegisterPasswordAccount(string email, string firstName, string lastName)
    {
        Email = email.Trim().ToLowerInvariant();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        CustomerType = CustomerType.Registered;
        RegisteredAtUtc = DateTime.UtcNow;
    }

    public void ConvertToRegistered(string email, string firstName, string lastName)
    {
        RegisterPasswordAccount(email, firstName, lastName);
    }

    public void ConvertToRegistered(string newEmail)
    {
        Email = newEmail.Trim().ToLowerInvariant();
        CustomerType = CustomerType.Registered;
        RegisteredAtUtc = DateTime.UtcNow;
    }

    public void ConvertToRegistered()
    {
        CustomerType = CustomerType.Registered;
        RegisteredAtUtc = DateTime.UtcNow;
    }

    public void UpdateConsent(bool consentGiven)
    {
        AnalyticsConsent = consentGiven;
        ConsentUpdatedAtUtc = DateTime.UtcNow;
        RaiseDomainEvent(new CustomerConsentUpdatedEvent(Id, consentGiven, ConsentUpdatedAtUtc.Value));
    }

    public void AddShippingAddress(Address address)
    {
        ArgumentNullException.ThrowIfNull(address, nameof(address));
        _shippingAddresses.Add(address);
    }

    public void Suspend(string reason, CustomerId suspendedBy)
    {
        if (Role == CustomerRole.SuperAdmin && Id == suspendedBy)
        {
            throw new BusinessRuleViolationException("SuperAdmin cannot suspend their own account.", nameof(Customer));
        }

        if (Status == CustomerStatus.Suspended)
        {
            return; // Idempotent
        }

        Status = CustomerStatus.Suspended;
        SuspendedAtUtc = DateTime.UtcNow;
        SuspensionReason = reason?.Trim();

        RaiseDomainEvent(new CustomerSuspendedEvent(Id, SuspensionReason ?? string.Empty, SuspendedAtUtc.Value, suspendedBy));
    }

    public void Reactivate(CustomerId reactivatedBy)
    {
        if (Status == CustomerStatus.Active)
        {
            return; // Idempotent
        }

        Status = CustomerStatus.Active;
        SuspendedAtUtc = null;
        SuspensionReason = null;

        RaiseDomainEvent(new CustomerReactivatedEvent(Id, DateTime.UtcNow, reactivatedBy));
    }

    public void ChangeRole(CustomerRole newRole, CustomerId changedBy)
    {
        if (newRole == CustomerRole.SuperAdmin)
        {
            throw new BusinessRuleViolationException("SuperAdmin role cannot be assigned via commands or endpoints.", nameof(Customer));
        }

        if (Role == CustomerRole.SuperAdmin && Id == changedBy && newRole != CustomerRole.SuperAdmin)
        {
            throw new BusinessRuleViolationException("SuperAdmin cannot demote their own account.", nameof(Customer));
        }

        if (Role == newRole)
        {
            return; // Idempotent
        }

        var previousRole = Role;
        Role = newRole;
        RoleChangedAtUtc = DateTime.UtcNow;
        RoleChangedByCustomerId = changedBy;

        RaiseDomainEvent(new CustomerRoleChangedEvent(Id, previousRole, newRole, changedBy, RoleChangedAtUtc.Value));
    }
}
