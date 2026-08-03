using FluentAssertions;
using Vendor.Infrastructure.Identity;

namespace Vendor.Infrastructure.Tests.Identity;

public class ApplicationUserTests
{
    [Fact]
    public void ApplicationUser_Initialization_SetsCustomerIdAndDefaultsCorrectly()
    {
        var customerId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "buyer@example.com",
            Email = "buyer@example.com",
            CustomerId = customerId
        };

        user.CustomerId.Should().Be(customerId);
        user.Email.Should().Be("buyer@example.com");
        user.CreatedAtUtc.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
    }
}
