using System.Collections.Generic;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Enums;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Tests;

public static class TestConfigFactory
{
    public static VendorConfig CreateValidVendorConfig()
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
