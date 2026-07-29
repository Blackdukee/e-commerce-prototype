using System.Collections.Generic;
using Vendor.Domain.Enums;

namespace Vendor.Domain.ValueObjects;

public sealed record BrandingConfig(
    string LogoUrl,
    string? FaviconUrl,
    string PrimaryColor,
    string SecondaryColor,
    string FontFamily,
    string MetaTitle,
    string MetaDescription,
    string? MetaKeywords);

public sealed record LocaleConfig(
    string DefaultLanguage,
    IReadOnlyList<string> SupportedLanguages,
    string DefaultCurrency,
    IReadOnlyList<string> SupportedCurrencies,
    string Timezone,
    TextDirection Direction);

public sealed record TaxConfig(
    TaxStrategy Strategy,
    decimal? FlatRatePercentage,
    bool PricesIncludeTax);

public sealed record CheckoutConfig(
    bool GuestCheckoutEnabled,
    int MaxItemsPerOrder,
    string OrderNumberPrefix);

public sealed record AuthConfig(
    int TokenLifetimeMinutes,
    int RefreshTokenLifetimeDays,
    SecretReference JwtSecret,
    string? GoogleClientId,
    SecretReference? GoogleClientSecret,
    string? FacebookAppId,
    SecretReference? FacebookAppSecret,
    int PasswordMinLength,
    bool PasswordRequireUppercase,
    bool PasswordRequireDigit,
    bool PasswordRequireSpecialChar);

public sealed record CachingConfig(
    CacheProvider Provider,
    SecretReference? RedisConnectionString,
    string? KeyPrefix);

public sealed record EmailConfig(
    EmailProvider Provider,
    string SenderAddress,
    string SenderName,
    SecretReference? MailtrapApiKey,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    SecretReference? SmtpPassword,
    IReadOnlyDictionary<string, string>? TemplateIds);

public sealed record AnalyticsConfig(
    string Provider,
    string TrackingId,
    bool ServerSideForwarding,
    SecretReference? ForwardingSecret,
    bool ConsentRequired,
    string? ConsentCookieName = "analytics_consent");

public sealed record PromotionsConfig(
    bool Enabled,
    int MaxDiscountCodesPerOrder,
    string EvaluationStrategy,
    bool AllowStacking);

public sealed record FeatureFlags(
    IReadOnlyDictionary<string, bool> Flags);

public sealed record PaymentCredentials(
    string PublicKey,
    SecretReference SecretKey);

public sealed record PaymentProviderConfig(
    string ProviderName,
    bool Enabled,
    bool IsDefault,
    PaymentCredentials Credentials,
    IReadOnlyList<string> SupportedMethods,
    CaptureMode CaptureMode,
    SecretReference WebhookSecret);

public sealed record ShippingProviderConfig(
    string ProviderName,
    bool Enabled,
    IReadOnlyDictionary<string, object> Settings,
    SecretReference? ApiKey);
