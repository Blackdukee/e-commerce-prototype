using FluentAssertions;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Xunit;

namespace Vendor.Domain.Tests.Aggregates;

public class CustomerAccountManagementTests
{
    [Fact]
    public void NewCustomer_HasDefaultRoleAndStatus()
    {
        var customer = new Customer(CustomerId.New(), "user@example.com", "John", "Doe");

        customer.Role.Should().Be(CustomerRole.Customer);
        customer.Status.Should().Be(CustomerStatus.Active);
        customer.SuspendedAtUtc.Should().BeNull();
        customer.SuspensionReason.Should().BeNull();
    }

    [Fact]
    public void Suspend_ValidCustomer_SetsSuspendedStatusAndEmitsEvent()
    {
        var customer = new Customer(CustomerId.New(), "user@example.com", "John", "Doe");
        var adminId = CustomerId.New();

        customer.Suspend("Terms violation", adminId);

        customer.Status.Should().Be(CustomerStatus.Suspended);
        customer.SuspensionReason.Should().Be("Terms violation");
        customer.SuspendedAtUtc.Should().NotBeNull();
        customer.DomainEvents.Should().ContainSingle(e => e is CustomerSuspendedEvent);
    }

    [Fact]
    public void Suspend_AlreadySuspendedCustomer_IsIdempotent()
    {
        var customer = new Customer(CustomerId.New(), "user@example.com", "John", "Doe");
        var adminId = CustomerId.New();

        customer.Suspend("Terms violation", adminId);
        var firstSuspendedAt = customer.SuspendedAtUtc;

        customer.Suspend("Terms violation", adminId); // Second call

        customer.Status.Should().Be(CustomerStatus.Suspended);
        customer.SuspendedAtUtc.Should().Be(firstSuspendedAt);
    }

    [Fact]
    public void Suspend_SuperAdminSelfSuspension_ThrowsBusinessRuleException()
    {
        var superAdminId = CustomerId.New();
        var superAdmin = new Customer(superAdminId, "super@example.com", "Super", "Admin", CustomerType.Registered, false, CustomerRole.SuperAdmin);

        var act = () => superAdmin.Suspend("Self suspend", superAdminId);

        act.Should().Throw<BusinessRuleViolationException>()
           .WithMessage("*SuperAdmin cannot suspend their own account*");
    }

    [Fact]
    public void Reactivate_SuspendedCustomer_SetsActiveStatusAndEmitsEvent()
    {
        var customer = new Customer(CustomerId.New(), "user@example.com", "John", "Doe");
        var adminId = CustomerId.New();
        customer.Suspend("Violation", adminId);

        customer.Reactivate(adminId);

        customer.Status.Should().Be(CustomerStatus.Active);
        customer.SuspendedAtUtc.Should().BeNull();
        customer.SuspensionReason.Should().BeNull();
        customer.DomainEvents.Should().Contain(e => e is CustomerReactivatedEvent);
    }

    [Fact]
    public void ChangeRole_ToAdmin_UpdatesRoleAndEmitsEvent()
    {
        var customer = new Customer(CustomerId.New(), "user@example.com", "John", "Doe");
        var superAdminId = CustomerId.New();

        customer.ChangeRole(CustomerRole.Admin, superAdminId);

        customer.Role.Should().Be(CustomerRole.Admin);
        customer.RoleChangedAtUtc.Should().NotBeNull();
        customer.RoleChangedByCustomerId.Should().Be(superAdminId);
        customer.DomainEvents.Should().Contain(e => e is CustomerRoleChangedEvent);
    }

    [Fact]
    public void ChangeRole_ToSuperAdmin_ThrowsException()
    {
        var customer = new Customer(CustomerId.New(), "user@example.com", "John", "Doe");
        var superAdminId = CustomerId.New();

        var act = () => customer.ChangeRole(CustomerRole.SuperAdmin, superAdminId);

        act.Should().Throw<BusinessRuleViolationException>()
           .WithMessage("*SuperAdmin role cannot be assigned*");
    }

    [Fact]
    public void ChangeRole_SuperAdminSelfDemotion_ThrowsException()
    {
        var superAdminId = CustomerId.New();
        var superAdmin = new Customer(superAdminId, "super@example.com", "Super", "Admin", CustomerType.Registered, false, CustomerRole.SuperAdmin);

        var act = () => superAdmin.ChangeRole(CustomerRole.Customer, superAdminId);

        act.Should().Throw<BusinessRuleViolationException>()
           .WithMessage("*SuperAdmin cannot demote their own account*");
    }
}
