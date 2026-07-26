using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Vendor.Application.DTOs;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Interfaces;

namespace Vendor.Application.Commands.VendorSettings;

public sealed record UpdateVendorSettingsCommand(
    string VendorId,
    VendorRuntimeConfig NewRuntimeConfig,
    int ExpectedVersion,
    string ModifiedBy) : IRequest<VendorConfigDto>;

public sealed class UpdateVendorSettingsHandler : IRequestHandler<UpdateVendorSettingsCommand, VendorConfigDto>
{
    private readonly VendorConfig _baseConfig;
    private readonly IVendorSettingsRepository _repository;
    private readonly IValidator<VendorConfig> _configValidator;

    public UpdateVendorSettingsHandler(
        VendorConfig baseConfig,
        IVendorSettingsRepository repository,
        IValidator<VendorConfig> configValidator)
    {
        _baseConfig = baseConfig;
        _repository = repository;
        _configValidator = configValidator;
    }

    public async Task<VendorConfigDto> Handle(UpdateVendorSettingsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Construct merged candidate config to validate full business rules
        var candidateConfig = new VendorConfig(
            _baseConfig.VendorId,
            _baseConfig.VendorDisplayName,
            _baseConfig.Build,
            _baseConfig.Boot,
            request.NewRuntimeConfig);

        // 2. Validate merged config against all rules
        var validationResult = await _configValidator.ValidateAsync(candidateConfig, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 3. Persist updated runtime config to DB
        await _repository.UpdateRuntimeConfigAsync(
            request.VendorId,
            request.NewRuntimeConfig,
            request.ExpectedVersion,
            request.ModifiedBy,
            cancellationToken);

        var newVersion = await _repository.GetVersionAsync(request.VendorId, cancellationToken);
        var tiers = new VendorTiersDto(_baseConfig.Build, _baseConfig.Boot, request.NewRuntimeConfig);

        return new VendorConfigDto(_baseConfig.VendorId, _baseConfig.VendorDisplayName, tiers, newVersion, DateTime.UtcNow);
    }
}
