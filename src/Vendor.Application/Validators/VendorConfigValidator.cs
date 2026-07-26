using System.Linq;
using FluentValidation;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Enums;

namespace Vendor.Application.Validators;

public sealed class VendorConfigValidator : AbstractValidator<VendorConfig>
{
    public VendorConfigValidator()
    {
        RuleFor(x => x.VendorId)
            .NotEmpty().WithMessage("VendorId is required.")
            .Matches("^[a-z0-9\\-]+$").WithMessage("VendorId must be lowercase alphanumeric with hyphens.");

        RuleFor(x => x.VendorDisplayName)
            .NotEmpty().WithMessage("VendorDisplayName is required.")
            .MaximumLength(128).WithMessage("VendorDisplayName cannot exceed 128 characters.");

        RuleFor(x => x.Runtime.Locale)
            .Custom((locale, context) =>
            {
                if (locale == null) return;
                if (!locale.SupportedCurrencies.Contains(locale.DefaultCurrency))
                {
                    context.AddFailure("locale.defaultCurrency", "Default currency must be in supported currencies list.");
                }
                if (!locale.SupportedLanguages.Contains(locale.DefaultLanguage))
                {
                    context.AddFailure("locale.defaultLanguage", "Default language must be in supported languages list.");
                }
            });

        RuleFor(x => x.Runtime.Payments)
            .NotEmpty().WithMessage("At least one payment provider configuration is required.")
            .Must(payments => payments != null && payments.Count(p => p.IsDefault) == 1)
            .WithMessage("Exactly one payment provider must be marked as default.");

        RuleFor(x => x.Boot.Caching)
            .Custom((caching, context) =>
            {
                if (caching == null) return;
                if (caching.Provider == CacheProvider.Redis && caching.RedisConnectionString == null)
                {
                    context.AddFailure("boot.caching.redisConnectionString", "Redis connection string required when using Redis provider.");
                }
            });

        RuleFor(x => x.Boot.Email)
            .Custom((email, context) =>
            {
                if (email == null) return;
                if (email.Provider == EmailProvider.SendGrid && email.SendGridApiKey == null)
                {
                    context.AddFailure("boot.email.sendGridApiKey", "SendGrid API key required when using SendGrid provider.");
                }
                if (email.Provider == EmailProvider.Smtp)
                {
                    if (string.IsNullOrWhiteSpace(email.SmtpHost) || !email.SmtpPort.HasValue)
                    {
                        context.AddFailure("boot.email.smtp", "SMTP host and port required when using SMTP provider.");
                    }
                }
            });
    }
}
