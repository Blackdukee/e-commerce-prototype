using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Infrastructure.Identity;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Auth;

public class IdentityAuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ICustomerRepository customerRepository,
    VendorDbContext dbContext)
    : IIdentityAuthService
{
    public async Task<IdentityRegisterResult> RegisterAsync(string email, string password, string firstName, string lastName, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (await customerRepository.EmailExistsAsync(normalizedEmail, ct))
        {
            return new IdentityRegisterResult(false, Guid.Empty, Guid.Empty, "Email.AlreadyRegistered", $"Email '{email}' is already registered.");
        }

        return await ExecuteInTransactionScopeAsync(async (tx) =>
        {
            var customerId = CustomerId.New();
            var customer = new Customer(customerId, normalizedEmail, firstName, lastName, CustomerType.Registered);
            await customerRepository.AddAsync(customer, ct);
            await dbContext.SaveChangesAsync(ct);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = normalizedEmail,
                Email = normalizedEmail,
                CustomerId = customerId.Value,
                CreatedAtUtc = DateTime.UtcNow
            };

            var identityResult = await userManager.CreateAsync(user, password);
            if (!identityResult.Succeeded)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                var firstError = identityResult.Errors.FirstOrDefault()?.Description ?? "User creation failed.";
                return new IdentityRegisterResult(false, Guid.Empty, Guid.Empty, "Auth.RegistrationFailed", firstError);
            }

            return new IdentityRegisterResult(true, user.Id, customerId.Value, null, null);
        }, ct);
    }

    public async Task<IdentitySignInResult> PasswordSignInAsync(string email, string password, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return new IdentitySignInResult(false, Guid.Empty, Guid.Empty, false, false, "Auth.InvalidCredentials", "Invalid email or password.");
        }

        var customer = await customerRepository.GetByIdAsync(new CustomerId(user.CustomerId), ct);
        if (customer is null)
        {
            return new IdentitySignInResult(false, Guid.Empty, Guid.Empty, false, false, "Customer.NotFound", "Customer aggregate not found.");
        }

        if (customer.Status == CustomerStatus.Suspended)
        {
            return new IdentitySignInResult(false, user.Id, user.CustomerId, false, false, "ACCOUNT_SUSPENDED", "Customer account is suspended.");
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return new IdentitySignInResult(false, user.Id, user.CustomerId, true, !user.EmailConfirmed, "Auth.LockedOut", "Account is locked out due to multiple failed login attempts.");
        }

        if (!result.Succeeded)
        {
            return new IdentitySignInResult(false, user.Id, user.CustomerId, false, !user.EmailConfirmed, "Auth.InvalidCredentials", "Invalid email or password.");
        }

        return new IdentitySignInResult(true, user.Id, user.CustomerId, false, !user.EmailConfirmed, null, null);
    }

    public async Task<IdentitySignInResult> ExternalSignInOrRegisterAsync(
        string provider,
        string providerKey,
        string email,
        bool isEmailVerified,
        string firstName,
        string lastName,
        CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        // 1. Look up by provider key
        var user = await userManager.FindByLoginAsync(provider, providerKey);
        if (user is not null)
        {
            var customer = await customerRepository.GetByIdAsync(new CustomerId(user.CustomerId), ct);
            if (customer is not null && customer.Status == CustomerStatus.Suspended)
            {
                return new IdentitySignInResult(false, user.Id, user.CustomerId, false, false, "ACCOUNT_SUSPENDED", "Customer account is suspended.");
            }
            return new IdentitySignInResult(true, user.Id, user.CustomerId, false, !user.EmailConfirmed, null, null);
        }

        // 2. Look up existing user by email
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            if (!isEmailVerified)
            {
                return new IdentitySignInResult(
                    false,
                    existingUser.Id,
                    existingUser.CustomerId,
                    false,
                    false,
                    "Auth.UnverifiedEmailConflict",
                    "An account with this email address already exists. Please sign in with your password first to link your social account.");
            }

            var addLoginRes = await userManager.AddLoginAsync(existingUser, new UserLoginInfo(provider, providerKey, provider));
            if (!addLoginRes.Succeeded)
            {
                return new IdentitySignInResult(false, existingUser.Id, existingUser.CustomerId, false, false, "Auth.ExternalLoginFailed", "Failed to link external login.");
            }

            return new IdentitySignInResult(true, existingUser.Id, existingUser.CustomerId, false, !existingUser.EmailConfirmed, null, null);
        }

        // 3. Create new user and customer aggregate atomically in a single transaction
        return await ExecuteInTransactionScopeAsync(async (tx) =>
        {
            var customerId = CustomerId.New();
            var customer = new Customer(customerId, normalizedEmail, firstName, lastName, CustomerType.Registered);
            await customerRepository.AddAsync(customer, ct);
            await dbContext.SaveChangesAsync(ct);

            var newUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = normalizedEmail,
                Email = normalizedEmail,
                EmailConfirmed = isEmailVerified,
                CustomerId = customerId.Value,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createRes = await userManager.CreateAsync(newUser);
            if (!createRes.Succeeded)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return new IdentitySignInResult(false, Guid.Empty, Guid.Empty, false, false, "Auth.RegistrationFailed", "Failed to create identity user.");
            }

            var linkRes = await userManager.AddLoginAsync(newUser, new UserLoginInfo(provider, providerKey, provider));
            if (!linkRes.Succeeded)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return new IdentitySignInResult(false, Guid.Empty, Guid.Empty, false, false, "Auth.ExternalLoginFailed", "Failed to link external login provider.");
            }

            return new IdentitySignInResult(true, newUser.Id, customerId.Value, false, !newUser.EmailConfirmed, null, null);
        }, ct);
    }

    private async Task<T> ExecuteInTransactionScopeAsync<T>(
        Func<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?, Task<T>> operation,
        CancellationToken ct)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            return await operation(null);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await operation(transaction);
                if (dbContext.Database.CurrentTransaction != null)
                {
                    await transaction.CommitAsync(ct);
                }
                return result;
            }
            catch
            {
                if (dbContext.Database.CurrentTransaction != null)
                {
                    await transaction.RollbackAsync(ct);
                }
                throw;
            }
        });
    }

    public async Task<string> GenerateEmailConfirmationTokenAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null) return string.Empty;
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null) return false;
        var res = await userManager.ConfirmEmailAsync(user, token);
        return res.Succeeded;
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null) return string.Empty;
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            return false;

        var user = await userManager.FindByEmailAsync(email.Trim().ToLowerInvariant());
        if (user is null) return false;

        string decodedToken = token;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch
        {
        }

        var res = await userManager.ResetPasswordAsync(user, decodedToken, newPassword);
        if (res.Succeeded) return true;

        if (decodedToken != token)
        {
            var fallbackRes = await userManager.ResetPasswordAsync(user, token, newPassword);
            return fallbackRes.Succeeded;
        }

        return false;
    }
}
