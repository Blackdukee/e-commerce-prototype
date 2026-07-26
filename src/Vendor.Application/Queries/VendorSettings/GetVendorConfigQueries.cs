using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Vendor.Application.DTOs;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Interfaces;

namespace Vendor.Application.Queries.VendorSettings;

public sealed record GetVendorConfigQuery(string VendorId) : IRequest<VendorConfigDto>;

public sealed class GetVendorConfigHandler : IRequestHandler<GetVendorConfigQuery, VendorConfigDto>
{
    private readonly VendorConfig _baseConfig;
    private readonly IVendorSettingsRepository _repository;

    public GetVendorConfigHandler(VendorConfig baseConfig, IVendorSettingsRepository repository)
    {
        _baseConfig = baseConfig;
        _repository = repository;
    }

    public async Task<VendorConfigDto> Handle(GetVendorConfigQuery request, CancellationToken cancellationToken)
    {
        var dbRuntime = await _repository.GetRuntimeConfigAsync(request.VendorId, cancellationToken);
        var activeRuntime = dbRuntime ?? _baseConfig.Runtime;
        var version = await _repository.GetVersionAsync(request.VendorId, cancellationToken);

        var tiers = new VendorTiersDto(_baseConfig.Build, _baseConfig.Boot, activeRuntime);
        return new VendorConfigDto(_baseConfig.VendorId, _baseConfig.VendorDisplayName, tiers, version, DateTime.UtcNow);
    }
}
