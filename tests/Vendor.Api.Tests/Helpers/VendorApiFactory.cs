using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Api.Tests.Helpers;

public class VendorApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "vendor-test-signing-key-256-bits!!";
    public const string TestIssuer = "VendorApiTest";
    public const string TestAudience = "VendorApiTestClient";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace the SQL Server DbContext with an in-memory one for API tests
            var efDescriptors = services.Where(d =>
                d.ServiceType.Name.Contains("DbContext") ||
                d.ServiceType.Namespace?.Contains("EntityFrameworkCore") == true).ToList();

            foreach (var d in efDescriptors)
            {
                services.Remove(d);
            }

            services.AddDbContext<VendorDbContext>(options =>
            {
                options.UseInMemoryDatabase("VendorApiTest_" + Guid.NewGuid().ToString("N"));
            });

            // Override JWT validation to use the test signing key
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestIssuer,
                    ValidateAudience = true,
                    ValidAudience = TestAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(TestJwtSecret)),
                    ValidateLifetime = false
                };
            });
        });
    }
}
