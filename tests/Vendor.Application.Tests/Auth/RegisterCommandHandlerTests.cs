using FluentAssertions;
using Moq;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Auth;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Tests.Auth;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IIdentityAuthService> _identityAuthMock = new();
    private readonly Mock<ICustomerRepository> _customerRepoMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();

    [Fact]
    public async Task Handle_RegistrationFails_ReturnsFailureResult()
    {
        _identityAuthMock
            .Setup(i => i.RegisterAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityRegisterResult(false, Guid.Empty, Guid.Empty, "Email.AlreadyRegistered", "Email already registered."));

        var handler = new RegisterCustomerCommandHandler(_identityAuthMock.Object, _customerRepoMock.Object, _tokenServiceMock.Object);
        var command = new RegisterCustomerCommand("existing@example.com", "Password123!", "Jane", "Doe");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.AlreadyRegistered");
    }

    [Fact]
    public async Task Handle_ValidRegistration_ReturnsAuthResponseWithTokens()
    {
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _identityAuthMock
            .Setup(i => i.RegisterAsync("new@example.com", "Password123!", "Jane", "Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityRegisterResult(true, userId, customerId, null, null));

        var customer = new Customer(new CustomerId(customerId), "new@example.com", "Jane", "Doe", CustomerType.Registered);
        _customerRepoMock
            .Setup(c => c.GetByIdAsync(new CustomerId(customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        _tokenServiceMock
            .Setup(t => t.GenerateTokens(customerId, "new@example.com", It.IsAny<IEnumerable<string>>()))
            .Returns(new TokenResult("access_token_123", "refresh_token_123", DateTime.UtcNow.AddHours(1)));

        var handler = new RegisterCustomerCommandHandler(_identityAuthMock.Object, _customerRepoMock.Object, _tokenServiceMock.Object);
        var command = new RegisterCustomerCommand("new@example.com", "Password123!", "Jane", "Doe");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access_token_123");
        result.Value.User.Id.Should().Be(customerId);
    }
}
