using FluentAssertions;
using Moq;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Auth;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Tests.Auth;

public class ExternalLoginCommandHandlerTests
{
    private readonly Mock<IExternalAuthService> _externalAuthMock = new();
    private readonly Mock<IIdentityAuthService> _identityAuthMock = new();
    private readonly Mock<ICustomerRepository> _customerRepoMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();

    [Fact]
    public async Task Handle_UnverifiedEmailConflict_ReturnsConflictError()
    {
        _externalAuthMock
            .Setup(e => e.VerifyGoogleTokenAsync("unverified_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalAuthUser("google_123", "existing@example.com", "Google", "User"));

        _identityAuthMock
            .Setup(i => i.ExternalSignInOrRegisterAsync("google", "google_123", "existing@example.com", true, "Google", "User", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentitySignInResult(false, Guid.NewGuid(), Guid.NewGuid(), false, false, "Auth.UnverifiedEmailConflict", "Email is not verified by provider. Please sign in with password first."));

        var handler = new LoginWithOAuthCommandHandler(_externalAuthMock.Object, _identityAuthMock.Object, _customerRepoMock.Object, _tokenServiceMock.Object);
        var command = new LoginWithOAuthCommand("google", "unverified_token");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UnverifiedEmailConflict");
    }

    [Fact]
    public async Task Handle_ValidExternalToken_ReturnsAuthTokens()
    {
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _externalAuthMock
            .Setup(e => e.VerifyGoogleTokenAsync("valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalAuthUser("google_999", "newuser@example.com", "Jane", "Doe"));

        _identityAuthMock
            .Setup(i => i.ExternalSignInOrRegisterAsync("google", "google_999", "newuser@example.com", true, "Jane", "Doe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentitySignInResult(true, userId, customerId, false, false, null, null));

        var customer = new Customer(new CustomerId(customerId), "newuser@example.com", "Jane", "Doe", CustomerType.Registered);
        _customerRepoMock
            .Setup(c => c.GetByIdAsync(new CustomerId(customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        _tokenServiceMock
            .Setup(t => t.GenerateTokens(customerId, "newuser@example.com", It.IsAny<IEnumerable<string>>()))
            .Returns(new TokenResult("oauth_access_123", "oauth_refresh_123", DateTime.UtcNow.AddHours(1)));

        var handler = new LoginWithOAuthCommandHandler(_externalAuthMock.Object, _identityAuthMock.Object, _customerRepoMock.Object, _tokenServiceMock.Object);
        var command = new LoginWithOAuthCommand("google", "valid_token");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("oauth_access_123");
    }
}
