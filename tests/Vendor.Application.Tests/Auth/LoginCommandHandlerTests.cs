using FluentAssertions;
using Moq;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Auth;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Tests.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IIdentityAuthService> _identityAuthMock = new();
    private readonly Mock<ICustomerRepository> _customerRepoMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();

    [Fact]
    public async Task Handle_AccountLockedOut_ReturnsLockedOutError()
    {
        _identityAuthMock
            .Setup(i => i.PasswordSignInAsync("locked@example.com", "wrong_pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentitySignInResult(false, Guid.NewGuid(), Guid.NewGuid(), IsLockedOut: true, IsUnverifiedEmail: false, "Auth.LockedOut", "Locked out"));

        var handler = new LoginWithPasswordCommandHandler(_identityAuthMock.Object, _customerRepoMock.Object, _tokenServiceMock.Object);
        var command = new LoginWithPasswordCommand("locked@example.com", "wrong_pass");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.LockedOut");
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _identityAuthMock
            .Setup(i => i.PasswordSignInAsync("valid@example.com", "Password123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentitySignInResult(true, userId, customerId, IsLockedOut: false, IsUnverifiedEmail: false, null, null));

        var customer = new Customer(new CustomerId(customerId), "valid@example.com", "Jane", "Doe", CustomerType.Registered);
        _customerRepoMock
            .Setup(c => c.GetByIdAsync(new CustomerId(customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        _tokenServiceMock
            .Setup(t => t.GenerateTokens(customerId, "valid@example.com", It.IsAny<IEnumerable<string>>()))
            .Returns(new TokenResult("access_123", "refresh_123", DateTime.UtcNow.AddHours(1)));

        var handler = new LoginWithPasswordCommandHandler(_identityAuthMock.Object, _customerRepoMock.Object, _tokenServiceMock.Object);
        var command = new LoginWithPasswordCommand("valid@example.com", "Password123!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access_123");
    }
}
