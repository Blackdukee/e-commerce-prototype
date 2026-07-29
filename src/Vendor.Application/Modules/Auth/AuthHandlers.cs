using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.Customer;
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
    ICustomerRepository customerRepository,
    ITokenService tokenService)
    : IRequestHandler<RegisterCustomerCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(RegisterCustomerCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await customerRepository.EmailExistsAsync(normalizedEmail, ct))
        {
            return Error.Conflict("Email.AlreadyRegistered", $"Email '{request.Email}' is already registered.");
        }

        var customer = new Customer(CustomerId.New(), normalizedEmail, request.FirstName, request.LastName, CustomerType.Registered);
        await customerRepository.AddAsync(customer, ct);

        var tokenResult = tokenService.GenerateTokens(customer.Id.Value, customer.Email, [customer.Role.ToString()]);
        var customerDto = new CustomerDto(customer.Id.Value, customer.Email, customer.FirstName, customer.LastName, customer.CustomerType.ToString(), customer.AnalyticsConsent);

        return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
    }
}

public class LoginWithPasswordCommandHandler(
    ICustomerRepository customerRepository,
    ITokenService tokenService)
    : IRequestHandler<LoginWithPasswordCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(LoginWithPasswordCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var customer = await customerRepository.GetByEmailAsync(normalizedEmail, ct);
        if (customer == null)
        {
            return Error.Unauthorized("Invalid email or password.");
        }

        if (customer.Status == CustomerStatus.Suspended)
        {
            return Error.Forbidden("ACCOUNT_SUSPENDED", "Customer account is suspended.");
        }

        var tokenResult = tokenService.GenerateTokens(customer.Id.Value, customer.Email, [customer.Role.ToString()]);
        var customerDto = new CustomerDto(customer.Id.Value, customer.Email, customer.FirstName, customer.LastName, customer.CustomerType.ToString(), customer.AnalyticsConsent);

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

        var normalizedEmail = externalUser.Email.Trim().ToLowerInvariant();
        var customer = await customerRepository.GetByEmailAsync(normalizedEmail, ct);
        if (customer == null)
        {
            customer = new Customer(CustomerId.New(), normalizedEmail, externalUser.FirstName, externalUser.LastName, CustomerType.Registered);
            await customerRepository.AddAsync(customer, ct);
        }

        if (customer.Status == CustomerStatus.Suspended)
        {
            return Error.Forbidden("ACCOUNT_SUSPENDED", "Customer account is suspended.");
        }

        var tokenResult = tokenService.GenerateTokens(customer.Id.Value, customer.Email, [customer.Role.ToString()]);
        var customerDto = new CustomerDto(customer.Id.Value, customer.Email, customer.FirstName, customer.LastName, customer.CustomerType.ToString(), customer.AnalyticsConsent);

        return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
    }
}

public class RefreshTokenCommandHandler(
    ITokenService tokenService,
    ICustomerRepository customerRepository)
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

public class ForgotPasswordCommandHandler(ICustomerRepository customerRepository)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        _ = await customerRepository.GetByEmailAsync(normalizedEmail, ct);
        // Always succeed to prevent user enumeration
        return Result.Success();
    }
}

public class ResetPasswordCommandHandler(ICustomerRepository customerRepository)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var customer = await customerRepository.GetByEmailAsync(normalizedEmail, ct);
        if (customer == null)
        {
            return Error.NotFound("Customer.NotFound", "Customer not found.");
        }

        return Result.Success();
    }
}
