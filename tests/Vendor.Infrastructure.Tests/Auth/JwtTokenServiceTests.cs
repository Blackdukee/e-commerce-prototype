using FluentAssertions;
using Vendor.Infrastructure.Auth;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Tests.Fixtures;

namespace Vendor.Infrastructure.Tests.Auth;

[Collection("Database")]
public class JwtTokenServiceTests : IAsyncLifetime
{
    private readonly MsSqlFixture _fixture;

    public JwtTokenServiceTests(MsSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void GenerateTokens_ValidParameters_ReturnsTokensAndStoresRefreshTokenInDb()
    {
        using var dbContext = new VendorDbContext(_fixture.DbContextOptions);
        var secret = "super-secret-key-that-is-at-least-32-chars-long!";
        var service = new JwtTokenService(dbContext, secret);

        var userId = Guid.NewGuid();
        var tokenResult = service.GenerateTokens(userId, "admin@example.com", ["Admin"]);

        tokenResult.Should().NotBeNull();
        tokenResult.AccessToken.Should().NotBeNullOrEmpty();
        tokenResult.RefreshToken.Should().NotBeNullOrEmpty();

        var storedToken = dbContext.RefreshTokens.FirstOrDefault(t => t.Token == tokenResult.RefreshToken);
        storedToken.Should().NotBeNull();
        storedToken!.CustomerId.Should().Be(userId);
        storedToken.IsRevoked.Should().BeFalse();
    }
}
