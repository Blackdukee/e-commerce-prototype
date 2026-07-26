namespace Vendor.Application.Interfaces;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IIdempotencyStore
{
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<TResponse?> GetResultAsync<TResponse>(string key, CancellationToken ct = default);
    Task SaveResultAsync<TResponse>(string key, TResponse result, CancellationToken ct = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public interface ICurrentUserService
{
    string? UserId { get; }
    Guid? CustomerId { get; }
    string VendorId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
}

public record TokenResult(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc);

public interface ITokenService
{
    TokenResult GenerateTokens(Guid userId, string email, IEnumerable<string> roles);
    Task<TokenResult?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
}

public record ExternalAuthUser(string ProviderId, string Email, string FirstName, string LastName);

public interface IExternalAuthService
{
    Task<ExternalAuthUser?> VerifyGoogleTokenAsync(string idToken, CancellationToken ct = default);
    Task<ExternalAuthUser?> VerifyFacebookTokenAsync(string accessToken, CancellationToken ct = default);
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
