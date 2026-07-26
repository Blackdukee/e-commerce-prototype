using FluentAssertions;
using Moq;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Application.Tests.Handlers;

public class RegisterCustomerHandlerTests
{
    [Fact]
    public void CustomerRegistration_WithValidData_CreatesRegisteredCustomer()
    {
        var customerId = CustomerId.New();
        var email = "john.doe@example.com";

        var customer = new Customer(
            customerId,
            email,
            "John",
            "Doe",
            CustomerType.Registered,
            analyticsConsent: true);

        customer.Email.Should().Be("john.doe@example.com");
        customer.CustomerType.Should().Be(CustomerType.Registered);
        customer.RegisteredAtUtc.Should().NotBeNull();
        customer.AnalyticsConsent.Should().BeTrue();
    }
}
