namespace Vendor.Api.DTOs;

public record RegisterRequest(string Email, string FirstName, string LastName, string Password);
public record LoginRequest(string Email, string Password);
public record GuestSessionRequest(string? SessionId);
public record RefreshTokenRequest(string RefreshToken);
public record RevokeTokenRequest(string RefreshToken);
public record ExternalAuthRequest(string Provider, string IdToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, CustomerDto Customer);

public record CustomerDto(Guid Id, string Email, string FirstName, string LastName, string CustomerType, bool AnalyticsConsent);
public record AddressDto(string Street, string City, string State, string ZipCode, string CountryCode);
public record UpdateConsentRequest(bool Granted);
public record ConvertGuestRequest(string Email, string Password);
