using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vendor.Application.Interfaces;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Enums;
using Vendor.Domain.Interfaces;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Auth;
using Vendor.Infrastructure.Common;
using Vendor.Infrastructure.Outbox;
using Vendor.Infrastructure.Payments;
using Vendor.Infrastructure.Persistence;
using Vendor.Infrastructure.Persistence.Repositories;
using Vendor.Infrastructure.Tax;

namespace Vendor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<OutboxInterceptor>();

        services.AddDbContext<VendorDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<OutboxInterceptor>();
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=VendorDb;Trusted_Connection=True;";

            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            options.AddInterceptors(interceptor);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<VendorDbContext>());
        services.AddScoped<IIdempotencyStore, DbIdempotencyStore>();

        // Register Repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IReturnRequestRepository, ReturnRequestRepository>();
        services.AddScoped<IAnalyticsEventRepository, AnalyticsEventRepository>();
        services.AddScoped<IVendorSettingsRepository, VendorSettingsRepository>();

        // Register Adapters and Services
        services.AddHttpClient();
        services.AddSingleton<StripePaymentGateway>();
        services.AddSingleton<PayPalPaymentGateway>();
        services.AddSingleton<PaymobPaymentGateway>();
        services.AddSingleton<IPaymentGatewayFactory, PaymentGatewayFactory>();
        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<ITaxCalculator, FlatTaxCalculator>();

        var jwtSecret = configuration["Jwt:SecretKey"] ?? "super-secret-jwt-key-minimum-32-characters-long!";
        services.AddScoped<ITokenService>(sp => new JwtTokenService(sp.GetRequiredService<VendorDbContext>(), jwtSecret));
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Default VendorConfig singleton for boot
        services.AddSingleton(CreateDefaultVendorConfig());

        return services;
    }

    private static VendorConfig CreateDefaultVendorConfig()
    {
        var build = new VendorBuildConfig("acme-store");
        var boot = new VendorBootConfig(
            new AuthConfig(60, 30, new SecretReference("ref:env:JWT_SECRET"), null, null, null, null, 8, true, true, false),
            new CachingConfig(CacheProvider.Memory, null, "acme"),
            new EmailConfig(EmailProvider.SendGrid, "noreply@acme.com", "ACME", new SecretReference("ref:env:SG_KEY"), null, null, null, null, null),
            new AnalyticsConfig("ga4", "G-12345", false, null, true)
        );
        var runtime = new VendorRuntimeConfig(
            new BrandingConfig("https://logo.svg", null, "#2563EB", "#1E40AF", "Inter", "Meta Title", "Meta Desc", null),
            new LocaleConfig("en", new[] { "en", "ar" }, "USD", new[] { "USD", "EUR" }, "UTC", TextDirection.Ltr),
            new TaxConfig(TaxStrategy.Flat, 8.875m, false),
            new CheckoutConfig(true, 50, "ACM"),
            new[]
            {
                new PaymentProviderConfig("stripe", true, true, new PaymentCredentials("pk_test", new SecretReference("ref:env:STRIPE_SK")), new[] { "card" }, CaptureMode.Automatic, new SecretReference("ref:env:STRIPE_WH"))
            },
            new[]
            {
                new ShippingProviderConfig("flat-rate", true, new Dictionary<string, object>(), null)
            },
            new PromotionsConfig(true, 1, "best-discount", false),
            new FeatureFlags(new Dictionary<string, bool> { { "reviews", true } })
        );

        return new VendorConfig("acme-store", "ACME Store", build, boot, runtime);
    }
}
