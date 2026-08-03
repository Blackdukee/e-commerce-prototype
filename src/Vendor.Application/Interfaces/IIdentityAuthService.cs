namespace Vendor.Application.Interfaces;

public record IdentityRegisterResult(bool Success, Guid UserId, Guid CustomerId, string? ErrorCode, string? ErrorMessage);
public record IdentitySignInResult(bool Success, Guid UserId, Guid CustomerId, bool IsLockedOut, bool IsUnverifiedEmail, string? ErrorCode, string? ErrorMessage);

public interface IIdentityAuthService
{
    Task<IdentityRegisterResult> RegisterAsync(string email, string password, string firstName, string lastName, CancellationToken ct = default);
    Task<IdentitySignInResult> PasswordSignInAsync(string email, string password, CancellationToken ct = default);
    Task<IdentitySignInResult> ExternalSignInOrRegisterAsync(string provider, string providerKey, string email, bool isEmailVerified, string firstName, string lastName, CancellationToken ct = default);
    Task<string> GenerateEmailConfirmationTokenAsync(string email, CancellationToken ct = default);
    Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct = default);
    Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken ct = default);
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);
}
