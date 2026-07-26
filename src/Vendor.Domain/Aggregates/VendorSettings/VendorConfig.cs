using System;
using System.Collections.Generic;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.VendorSettings;

public sealed record VendorBuildConfig(string VendorId);

public sealed record VendorBootConfig(
    AuthConfig Auth,
    CachingConfig Caching,
    EmailConfig Email,
    AnalyticsConfig Analytics);

public sealed record VendorRuntimeConfig(
    BrandingConfig Branding,
    LocaleConfig Locale,
    TaxConfig Tax,
    CheckoutConfig Checkout,
    IReadOnlyList<PaymentProviderConfig> Payments,
    IReadOnlyList<ShippingProviderConfig> Shipping,
    PromotionsConfig Promotions,
    FeatureFlags FeatureFlags);

public sealed class VendorConfig
{
    public string VendorId { get; }
    public string VendorDisplayName { get; private set; }
    public VendorBuildConfig Build { get; }
    public VendorBootConfig Boot { get; }
    public VendorRuntimeConfig Runtime { get; private set; }

    public VendorConfig(
        string vendorId,
        string vendorDisplayName,
        VendorBuildConfig build,
        VendorBootConfig boot,
        VendorRuntimeConfig runtime)
    {
        if (string.IsNullOrWhiteSpace(vendorId))
            throw new ArgumentException("Vendor ID cannot be null or empty.", nameof(vendorId));
        if (string.IsNullOrWhiteSpace(vendorDisplayName))
            throw new ArgumentException("Vendor Display Name cannot be null or empty.", nameof(vendorDisplayName));

        VendorId = vendorId;
        VendorDisplayName = vendorDisplayName;
        Build = build ?? throw new ArgumentNullException(nameof(build));
        Boot = boot ?? throw new ArgumentNullException(nameof(boot));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public void UpdateRuntime(VendorRuntimeConfig newRuntime, string displayName)
    {
        Runtime = newRuntime ?? throw new ArgumentNullException(nameof(newRuntime));
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            VendorDisplayName = displayName;
        }
    }
}
