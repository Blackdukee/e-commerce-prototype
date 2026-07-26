using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Vendor.Api.Tests.Helpers;

public static class AuthHelper
{
    public static string GenerateToken(string userId, string role, int expirationMinutes = 60)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(VendorApiFactory.TestJwtSecret);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            Issuer = VendorApiFactory.TestIssuer,
            Audience = VendorApiFactory.TestAudience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public static string GenerateAdminToken(string adminId = "admin-001")
    {
        return GenerateToken(adminId, "VendorAdmin");
    }

    public static string GenerateCustomerToken(string customerId = "customer-001")
    {
        return GenerateToken(customerId, "Customer");
    }

    public static string GenerateExpiredToken(string userId = "user-001")
    {
        return GenerateToken(userId, "Customer", expirationMinutes: -60);
    }

    public static HttpClient WithAdminBearerToken(this HttpClient client, string adminId = "admin-001")
    {
        var token = GenerateAdminToken(adminId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static HttpClient WithCustomerBearerToken(this HttpClient client, string customerId = "customer-001")
    {
        var token = GenerateCustomerToken(customerId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
