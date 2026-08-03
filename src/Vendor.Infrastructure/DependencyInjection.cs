using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
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
using Vendor.Infrastructure.Caching;
using Vendor.Infrastructure.Common;
using Vendor.Infrastructure.Email;
using Vendor.Infrastructure.Identity;
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

        // Redis distributed cache — connection string read from ConnectionStrings:Redis
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Redis is required. Add it to appsettings or set the CONNECTIONSTRINGS__REDIS environment variable.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "vendor:";
        });

        // Bind ICacheService to the Redis implementation
        services.AddScoped<ICacheService, RedisCacheService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=VendorDb;Trusted_Connection=True;";

        services.AddDbContext<VendorDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<OutboxInterceptor>();

            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });

            options.AddInterceptors(interceptor);
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2;
        });

        services.AddScoped<OutboxProcessorJob>();
        services.AddScoped<OutboxCleanupJob>();

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<VendorDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<VendorDbContext>());
        services.AddScoped<IIdempotencyStore, DbIdempotencyStore>();

        // Register Repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentIdempotencyRepository, PaymentIdempotencyRepository>();
        services.AddScoped<IPaymentLedgerRepository, PaymentLedgerRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
        services.AddSingleton<Vendor.Application.Common.Interfaces.IIdempotencyLockManager, Vendor.Infrastructure.Payments.Concurrency.InMemoryIdempotencyLockManager>();
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

        // Resolve JWT secret from configuration — validated at startup by IOptions<JwtOptions> in the API layer
        var jwtSecret = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SecretKey configuration is required. Set it via environment variable or appsettings.");
        services.AddScoped<IIdentityAuthService, IdentityAuthService>();
        services.AddScoped<ITokenService>(sp =>
            new JwtTokenService(sp.GetRequiredService<VendorDbContext>(), jwtSecret));
        services.AddScoped<IExternalAuthService, ExternalAuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<INotificationSender>(sp =>
        {
            var config = sp.GetRequiredService<VendorConfig>();
            var emailConfig = config.Boot.Email;

            static string ResolveSecret(string? rawRef)
            {
                if (string.IsNullOrWhiteSpace(rawRef)) return "";
                if (rawRef.StartsWith("ref:env:", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = rawRef["ref:env:".Length..];
                    return Environment.GetEnvironmentVariable(varName) ?? rawRef;
                }
                return rawRef;
            }

            if (emailConfig.Provider == EmailProvider.Smtp)
            {
                var smtpPassword = ResolveSecret(emailConfig.SmtpPassword?.RawReference);
                return new SmtpEmailSender(
                    emailConfig.SmtpHost ?? "localhost",
                    emailConfig.SmtpPort ?? 25,
                    emailConfig.SmtpUsername ?? "",
                    smtpPassword,
                    emailConfig.SenderAddress,
                    emailConfig.SenderName);
            }

            var apiToken = ResolveSecret(emailConfig.MailtrapApiKey?.RawReference);
            return new MailtrapEmailSender(
                apiToken,
                emailConfig.SenderAddress,
                emailConfig.SenderName);
        });

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
            new EmailConfig(EmailProvider.Mailtrap, "noreply@acme.com", "ACME", new SecretReference("ref:env:MAILTRAP_API_KEY"), null, null, null, null, null),
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
