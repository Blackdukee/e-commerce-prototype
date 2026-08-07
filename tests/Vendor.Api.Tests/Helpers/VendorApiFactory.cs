using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Api.Tests.Helpers;

public class VendorApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtSecret = "vendor-test-signing-key-256-bits!!";
    public const string TestIssuer = "VendorApiTest";
    public const string TestAudience = "VendorApiTestClient";

    private readonly string _dbName = "VendorApiTest_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            // Pin connection strings: EF DbContext is replaced below with in-memory,
            // but Hangfire still needs a real SQL Server — keep it on LocalDB so
            // the Docker MSSQL container is never required during test runs.
            // Webhook secrets are pinned so real API keys in appsettings.Development.json
            // do not break integration test signature verification.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost,14330;Database=VendorDb;User Id=sa;Password=YourStr0ng!Pass;TrustServerCertificate=True;",
                ["ConnectionStrings:Redis"]             = "",
                ["Stripe:WebhookSecret"]                = "whsec_test_secret_12345",
                ["Paymob:HmacSecret"]                   = "paymob_hmac_secret_test",
                ["Paypal:WebhookId"]                    = "paypal_wh_id_test",
                ["AWS:AccessKey"]                       = "AKIA_TEST_KEY_12345",
                ["AWS:SecretKey"]                       = "aws_test_secret_key_1234567890",
                ["AWS:S3:BucketName"]                   = "e-commerce-test-bucket",
            });
        });

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
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });

            // Override health check registrations for tests so external SQL Server/Redis checks don't block tests
            services.Configure<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
                options.Registrations.Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
                    "test_db",
                    new TestHealthCheck(),
                    failureStatus: null,
                    tags: ["ready", "live"]));
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

    private sealed class TestHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
    {
        public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
        }
    }
}
