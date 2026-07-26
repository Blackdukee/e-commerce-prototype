using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Auth;

public record AuthResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, CustomerDto User);
public record CustomerDto(Guid Id, string Email, string FirstName, string LastName, string CustomerType, bool AnalyticsConsent);

public record RegisterCustomerCommand(string Email, string Password, string FirstName, string LastName) : ICommand<Result<AuthResponseDto>>;
public record LoginWithPasswordCommand(string Email, string Password) : ICommand<Result<AuthResponseDto>>;
public record LoginWithOAuthCommand(string Provider, string IdToken) : ICommand<Result<AuthResponseDto>>;
public record RefreshTokenCommand(string RefreshToken) : ICommand<Result<AuthResponseDto>>;
public record RevokeTokenCommand(string RefreshToken) : ICommand<Result>, IIdempotentRequest<Result>
{
    public string IdempotencyKey => $"REVOKE-{RefreshToken}";
}
public record ChangePasswordCommand(Guid CustomerId, string CurrentPassword, string NewPassword) : ICommand<Result>;

public record GetCurrentUserProfileQuery : IQuery<Result<CustomerDto>>;
public record ValidateTokenQuery(string Token) : IQuery<Result<bool>>;

public class RegisterCustomerCommandHandler(
    ICustomerRepository customerRepository,
    ITokenService tokenService)
    : IRequestHandler<RegisterCustomerCommand, Result<AuthResponseDto>>
{
    public async Task<Result<AuthResponseDto>> Handle(RegisterCustomerCommand request, CancellationToken ct)
    {
        if (await customerRepository.EmailExistsAsync(request.Email, ct))
        {
            return Error.Conflict("Email.AlreadyRegistered", $"Email '{request.Email}' is already registered.");
        }

        var customer = new Customer(CustomerId.New(), request.Email, request.FirstName, request.LastName, CustomerType.Registered);
        await customerRepository.AddAsync(customer, ct);

        var tokenResult = tokenService.GenerateTokens(customer.Id.Value, customer.Email, ["Customer"]);
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
        var customer = await customerRepository.GetByEmailAsync(request.Email, ct);
        if (customer == null)
        {
            return Error.Unauthorized("Invalid email or password.");
        }

        var tokenResult = tokenService.GenerateTokens(customer.Id.Value, customer.Email, ["Customer"]);
        var customerDto = new CustomerDto(customer.Id.Value, customer.Email, customer.FirstName, customer.LastName, customer.CustomerType.ToString(), customer.AnalyticsConsent);

        return new AuthResponseDto(tokenResult.AccessToken, tokenResult.RefreshToken, tokenResult.AccessTokenExpiresAtUtc, customerDto);
    }
}
