using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Interfaces;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Config;

public sealed class SecretResolutionFilter : IStartupFilter
{
    private readonly VendorConfig _config;
    private readonly ISecretResolver _secretResolver;
    private readonly ILogger<SecretResolutionFilter> _logger;

    public SecretResolutionFilter(
        VendorConfig config,
        ISecretResolver secretResolver,
        ILogger<SecretResolutionFilter> logger)
    {
        _config = config;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ResolveSecretsAsync().GetAwaiter().GetResult();
        return next;
    }

    private async Task ResolveSecretsAsync()
    {
        _logger.LogInformation("Resolving secret references for vendor: {VendorId}", _config.VendorId);

        var secretRefs = ExtractSecretReferences(_config);
        int count = 0;
        foreach (var sRef in secretRefs)
        {
            try
            {
                await _secretResolver.ResolveAsync(sRef);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to resolve secret reference: {SecretRef}", sRef.RawReference);
                throw new InvalidOperationException($"Fatal boot error: Could not resolve secret '{sRef.RawReference}'. Container halting.", ex);
            }
        }

        _logger.LogInformation("Successfully resolved {Count} secret references for vendor: {VendorId}", count, _config.VendorId);
    }

    private static List<SecretReference> ExtractSecretReferences(VendorConfig config)
    {
        var list = new List<SecretReference>();
        if (config.Boot?.Auth?.JwtSecret != null) list.Add(config.Boot.Auth.JwtSecret);
        if (config.Boot?.Auth?.GoogleClientSecret != null) list.Add(config.Boot.Auth.GoogleClientSecret);
        if (config.Boot?.Auth?.FacebookAppSecret != null) list.Add(config.Boot.Auth.FacebookAppSecret);
        if (config.Boot?.Caching?.RedisConnectionString != null) list.Add(config.Boot.Caching.RedisConnectionString);
        if (config.Boot?.Email?.SendGridApiKey != null) list.Add(config.Boot.Email.SendGridApiKey);
        if (config.Boot?.Email?.SmtpPassword != null) list.Add(config.Boot.Email.SmtpPassword);
        if (config.Boot?.Analytics?.ForwardingSecret != null) list.Add(config.Boot.Analytics.ForwardingSecret);

        if (config.Runtime?.Payments != null)
        {
            foreach (var p in config.Runtime.Payments)
            {
                if (p.Credentials?.SecretKey != null) list.Add(p.Credentials.SecretKey);
                if (p.WebhookSecret != null) list.Add(p.WebhookSecret);
            }
        }

        if (config.Runtime?.Shipping != null)
        {
            foreach (var s in config.Runtime.Shipping)
            {
                if (s.ApiKey != null) list.Add(s.ApiKey);
            }
        }

        return list;
    }
}

public sealed class VendorConfigValidationFilter : IStartupFilter
{
    private readonly VendorConfig _config;
    private readonly IValidator<VendorConfig> _validator;
    private readonly ILogger<VendorConfigValidationFilter> _logger;

    public VendorConfigValidationFilter(
        VendorConfig config,
        IValidator<VendorConfig> validator,
        ILogger<VendorConfigValidationFilter> logger)
    {
        _config = config;
        _validator = validator;
        _logger = logger;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        _logger.LogInformation("Validating vendor configuration for vendor: {VendorId}", _config.VendorId);
        var result = _validator.Validate(_config);

        if (!result.IsValid)
        {
            _logger.LogCritical("Vendor configuration validation FAILED for vendor: {VendorId}", _config.VendorId);
            foreach (var error in result.Errors)
            {
                _logger.LogCritical(" - {PropertyName}: {ErrorMessage}", error.PropertyName, error.ErrorMessage);
            }
            throw new ValidationException($"Fatal boot error: Vendor configuration is invalid. {result.Errors.Count} error(s) found.", result.Errors);
        }

        _logger.LogInformation("Vendor configuration validated successfully for vendor: {VendorId}", _config.VendorId);
        return next;
    }
}
