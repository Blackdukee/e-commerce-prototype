using System.Collections.Generic;
using FluentAssertions;
using Vendor.Application.Validators;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Enums;
using Vendor.Domain.ValueObjects;
using Xunit;

namespace Vendor.Application.Tests.Validators;

public class VendorConfigValidatorTests
{
    private readonly VendorConfigValidator _validator = new();

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

    [Fact]
    public void Validate_ValidConfig_ShouldPass()
    {
        var config = CreateValidVendorConfig();
        var result = _validator.Validate(config);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleDefaultPaymentProviders_ShouldFail()
    {
        var config = CreateValidVendorConfig();
        var invalidPayments = new[]
        {
            new PaymentProviderConfig("stripe", true, true, new PaymentCredentials("pk_test", new SecretReference("ref:env:SK1")), new[] { "card" }, CaptureMode.Automatic, new SecretReference("ref:env:WH1")),
            new PaymentProviderConfig("paypal", true, true, new PaymentCredentials("pk_test", new SecretReference("ref:env:SK2")), new[] { "paypal" }, CaptureMode.Automatic, new SecretReference("ref:env:WH2"))
        };
        var invalidRuntime = config.Runtime with { Payments = invalidPayments };
        var invalidConfig = new VendorConfig(config.VendorId, config.VendorDisplayName, config.Build, config.Boot, invalidRuntime);

        var result = _validator.Validate(invalidConfig);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Exactly one payment provider must be marked as default"));
    }

    [Fact]
    public void Validate_DefaultCurrencyNotInSupportedCurrencies_ShouldFail()
    {
        var config = CreateValidVendorConfig();
        var invalidLocale = config.Runtime.Locale with { DefaultCurrency = "GBP", SupportedCurrencies = new[] { "USD", "EUR" } };
        var invalidRuntime = config.Runtime with { Locale = invalidLocale };
        var invalidConfig = new VendorConfig(config.VendorId, config.VendorDisplayName, config.Build, config.Boot, invalidRuntime);

        var result = _validator.Validate(invalidConfig);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("defaultCurrency"));
    }

    [Fact]
    public void Validate_DefaultLanguageNotInSupportedLanguages_ShouldFail()
    {
        var config = CreateValidVendorConfig();
        var invalidLocale = config.Runtime.Locale with { DefaultLanguage = "fr", SupportedLanguages = new[] { "en", "ar" } };
        var invalidRuntime = config.Runtime with { Locale = invalidLocale };
        var invalidConfig = new VendorConfig(config.VendorId, config.VendorDisplayName, config.Build, config.Boot, invalidRuntime);

        var result = _validator.Validate(invalidConfig);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("defaultLanguage"));
    }
}
