using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Auth;

public record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, CustomerDto User);
public record CustomerDto(Guid Id, string Email, string FirstName, string LastName, string CustomerType, bool AnalyticsConsent);

public record RegisterCustomerCommand(string Email, string Password, string FirstName, string LastName) : ICommand<Result<AuthResponseDto>>;
public record LoginWithPasswordCommand(string Email, string Password) : ICommand<Result<AuthResponseDto>>;
public record CreateGuestSessionCommand(string? SessionId) : ICommand<Result<AuthResponseDto>>;
public record LoginWithOAuthCommand(string Provider, string IdToken) : ICommand<Result<AuthResponseDto>>;
public record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthResponseDto>>;
public record RevokeTokenCommand(string RefreshToken) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"REVOKE-{RefreshToken}";
}
public record ChangePasswordCommand(Guid CustomerId, string CurrentPassword, string NewPassword) : ICommand<Result>;
public record ForgotPasswordCommand(string Email) : ICommand<Result>;
public record ResetPasswordCommand(string Email, string Token, string NewPassword) : ICommand<Result>;

public record GetCurrentUserProfileQuery : IQuery<Result<CustomerDto>>;
public record ValidateTokenQuery(string Token) : IQuery<Result<bool>>;

public class RegisterCustomerCommandHandler(
    IIdentityAuthService identityAuthService,
    ICustomerRepository customerRepository,
    ITokenService tokenService)
    : IRequestHandler<RegisterCustomerCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(RegisterCustomerCommand request, CancellationToken ct)
    {
        var result = await identityAuthService.RegisterAsync(request.Email, request.Password, request.FirstName, request.LastName, ct);
        if (!result.Success)
        {
            if (result.ErrorCode == "Email.AlreadyRegistered")
            {
                return Error.Conflict("Email.AlreadyRegistered", result.ErrorMessage ?? $"Email '{request.Email}' is already registered.");
            }
            return Error.Failure(result.ErrorCode ?? "Auth.RegistrationFailed", result.ErrorMessage ?? "Registration failed.");
        }

        var customer = await customerRepository.GetByIdAsync(new CustomerId(result.CustomerId), ct);
        var tokenResult = tokenService.GenerateTokens(result.CustomerId, request.Email, [customer?.Role.ToString() ?? "Customer"]);
        var customerDto = new CustomerDto(result.CustomerId, request.Email, request.FirstName, request.LastName, "Registered", true);

        return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
    }
}

public class LoginWithPasswordCommandHandler(
    IIdentityAuthService identityAuthService,
    ICustomerRepository customerRepository,
    ITokenService tokenService)
    : IRequestHandler<LoginWithPasswordCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(LoginWithPasswordCommand request, CancellationToken ct)
    {
        var result = await identityAuthService.PasswordSignInAsync(request.Email, request.Password, ct);
        if (result.IsLockedOut)
        {
            return Error.Failure("Auth.LockedOut", "Account is locked out due to multiple failed login attempts.");
        }

        if (!result.Success)
        {
            if (result.ErrorCode == "ACCOUNT_SUSPENDED")
            {
                return Error.Forbidden("ACCOUNT_SUSPENDED", result.ErrorMessage ?? "Customer account is suspended.");
            }
            return Error.Unauthorized(result.ErrorMessage ?? "Invalid email or password.");
        }

        var customer = await customerRepository.GetByIdAsync(new CustomerId(result.CustomerId), ct);
        var firstName = customer?.FirstName ?? string.Empty;
        var lastName = customer?.LastName ?? string.Empty;

        var tokenResult = tokenService.GenerateTokens(result.CustomerId, request.Email, [customer?.Role.ToString() ?? "Customer"]);
        var customerDto = new CustomerDto(result.CustomerId, request.Email, firstName, lastName, "Registered", true);

        return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
    }
}

public class CreateGuestSessionCommandHandler(
    ICustomerRepository customerRepository,
    ITokenService tokenService)
    : IRequestHandler<CreateGuestSessionCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(CreateGuestSessionCommand request, CancellationToken ct)
    {
        var guestId = Guid.NewGuid();
        var guestEmail = $"guest_{guestId:N}@vendor.local";
        var customer = new Customer(new CustomerId(guestId), guestEmail, "Guest", "User", CustomerType.Guest);

        await customerRepository.AddAsync(customer, ct);

        var tokenResult = tokenService.GenerateTokens(customer.Id.Value, customer.Email, [customer.Role.ToString()]);
        var customerDto = new CustomerDto(customer.Id.Value, customer.Email, customer.FirstName, customer.LastName, customer.CustomerType.ToString(), customer.AnalyticsConsent);

        return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
    }
}

public class LoginWithOAuthCommandHandler(
    IExternalAuthService externalAuthService,
    IIdentityAuthService identityAuthService,
    ICustomerRepository customerRepository,
    ITokenService tokenService)
    : IRequestHandler<LoginWithOAuthCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(LoginWithOAuthCommand request, CancellationToken ct)
    {
        ExternalAuthUser? externalUser = request.Provider.ToLowerInvariant() switch
        {
            "google" => await externalAuthService.VerifyGoogleTokenAsync(request.IdToken, ct),
            "facebook" => await externalAuthService.VerifyFacebookTokenAsync(request.IdToken, ct),
            _ => null
        };

        if (externalUser == null)
        {
            return Error.Unauthorized($"Invalid or unverified {request.Provider} token.");
        }

        var result = await identityAuthService.ExternalSignInOrRegisterAsync(
            request.Provider,
            externalUser.ProviderId,
            externalUser.Email,
            isEmailVerified: true,
            externalUser.FirstName,
            externalUser.LastName,
            ct);

        if (!result.Success)
        {
            if (result.ErrorCode == "Auth.UnverifiedEmailConflict")
            {
                return Error.Conflict("Auth.UnverifiedEmailConflict", result.ErrorMessage ?? "Email is not verified by provider. Please sign in with password first.");
            }

            if (result.ErrorCode == "ACCOUNT_SUSPENDED")
            {
                return Error.Forbidden("ACCOUNT_SUSPENDED", result.ErrorMessage ?? "Customer account is suspended.");
            }

            return Error.Unauthorized(result.ErrorMessage ?? $"External login via {request.Provider} failed.");
        }

        var customer = await customerRepository.GetByIdAsync(new CustomerId(result.CustomerId), ct);
        var tokenResult = tokenService.GenerateTokens(result.CustomerId, externalUser.Email, [customer?.Role.ToString() ?? "Customer"]);
        var customerDto = new CustomerDto(result.CustomerId, externalUser.Email, externalUser.FirstName, externalUser.LastName, "Registered", true);

        return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
    }
}

public class RefreshTokenCommandHandler(
    ITokenService tokenService)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var tokenResult = await tokenService.RefreshTokenAsync(request.RefreshToken, ct);
        if (tokenResult == null)
        {
            return Error.Unauthorized("Invalid or expired refresh token.");
        }

        return new AuthResponseDto(
            tokenResult.AccessToken,
            tokenResult.RefreshToken,
            tokenResult.AccessTokenExpiresAtUtc,
            new CustomerDto(Guid.Empty, string.Empty, "User", string.Empty, "Registered", true));
    }
}

public class RevokeTokenCommandHandler(ITokenService tokenService)
    : IRequestHandler<RevokeTokenCommand, Result>
{
    public async Task<Result> Handle(RevokeTokenCommand request, CancellationToken ct)
    {
        await tokenService.RevokeTokenAsync(request.RefreshToken, ct);
        return Result.Success();
    }
}

public class ForgotPasswordCommandHandler(IIdentityAuthService identityAuthService, INotificationSender notificationSender)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var token = await identityAuthService.GeneratePasswordResetTokenAsync(request.Email, ct);
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                await notificationSender.SendPasswordResetAsync(request.Email, token, ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ForgotPassword] Exception sending reset email: {ex.Message}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ForgotPassword] User '{request.Email}' not found in database. Register first via POST /api/v1/auth/register.");
        }

        // Always succeed to prevent user enumeration
        return Result.Success();
    }
}

public class ResetPasswordCommandHandler(IIdentityAuthService identityAuthService)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var success = await identityAuthService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
        if (!success)
        {
            return Error.Failure("Auth.ResetPasswordFailed", "Failed to reset password. Invalid or expired token.");
        }

        return Result.Success();
    }
}
