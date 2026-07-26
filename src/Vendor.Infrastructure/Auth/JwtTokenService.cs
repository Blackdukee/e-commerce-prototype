using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Vendor.Application.Interfaces;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Auth;

public class JwtTokenService(
    VendorDbContext dbContext,
    string secretKey) : ITokenService
{
    public TokenResult GenerateTokens(Guid userId, string email, IEnumerable<string> roles)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey.Length >= 32 ? secretKey : secretKey.PadRight(32, '0'));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        var refreshTokenBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(refreshTokenBytes);
        var refreshToken = Convert.ToBase64String(refreshTokenBytes);

        var tokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            CustomerId = userId,
            Token = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.RefreshTokens.Add(tokenEntity);
        dbContext.SaveChanges();

        return new TokenResult(accessToken, refreshToken, expiresAt);
    }

    public async Task<TokenResult?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var existingToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked, ct);
        if (existingToken == null || existingToken.ExpiresAtUtc <= DateTime.UtcNow) return null;

        existingToken.IsRevoked = true;
        dbContext.RefreshTokens.Update(existingToken);

        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id.Value == existingToken.CustomerId, ct);
        if (customer == null || customer.Status == Domain.Aggregates.Customer.CustomerStatus.Suspended) return null;

        var newTokens = GenerateTokens(customer.Id.Value, customer.Email, ["Customer"]);
        await dbContext.SaveChangesAsync(ct);
        return newTokens;
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var existingToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, ct);
        if (existingToken != null)
        {
            existingToken.IsRevoked = true;
            dbContext.RefreshTokens.Update(existingToken);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAllTokensForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.CustomerId == userId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
        }

        if (activeTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey.Length >= 32 ? secretKey : secretKey.PadRight(32, '0'));

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
