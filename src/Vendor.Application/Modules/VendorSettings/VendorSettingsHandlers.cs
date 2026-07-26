using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;

namespace Vendor.Application.Modules.VendorSettings;

public record VendorConfigDto(string VendorId, string VendorDisplayName, int Version, DateTime LastModifiedUtc);

public record PatchVendorRuntimeSettingsCommand(string RuntimeConfigPatchJson, int ExpectedVersion) : ICommand<Result<VendorConfigDto>>, IIdempotentRequest<Result<VendorConfigDto>>
{
    public string IdempotencyKey => $"PATCH-VENDOR-{ExpectedVersion}-{RuntimeConfigPatchJson.GetHashCode()}";
}

public record GetVendorConfigQuery : IQuery<Result<VendorConfigDto>>;
public record GetVendorConfigSchemaQuery : IQuery<Result<string>>;

public class GetVendorConfigQueryHandler : IRequestHandler<GetVendorConfigQuery, Result<VendorConfigDto>>
{
    public Task<Result<VendorConfigDto>> Handle(GetVendorConfigQuery request, CancellationToken ct)
    {
        var dto = new VendorConfigDto("acme-store", "ACME Store", 1, DateTime.UtcNow);
        return Task.FromResult(Result<VendorConfigDto>.Success(dto));
    }
}
