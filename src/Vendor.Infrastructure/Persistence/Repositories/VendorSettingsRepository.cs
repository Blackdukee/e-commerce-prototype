using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vendor.Domain.Aggregates.VendorSettings;
using Vendor.Domain.Interfaces;
using Vendor.Infrastructure.Persistence.Entities;

namespace Vendor.Infrastructure.Persistence.Repositories;

public sealed class VendorSettingsRepository(VendorDbContext dbContext) : IVendorSettingsRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<VendorRuntimeConfig?> GetRuntimeConfigAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.VendorSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.VendorId == vendorId, cancellationToken);

        if (settings == null || string.IsNullOrWhiteSpace(settings.RuntimeConfigJson))
            return null;

        return JsonSerializer.Deserialize<VendorRuntimeConfig>(settings.RuntimeConfigJson, JsonOpts);
    }

    public async Task<int> GetVersionAsync(string vendorId, CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.VendorSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.VendorId == vendorId, cancellationToken);

        return settings?.Version ?? 1;
    }

    public async Task UpdateRuntimeConfigAsync(
        string vendorId,
        VendorRuntimeConfig runtimeConfig,
        int expectedVersion,
        string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.VendorSettings
            .FirstOrDefaultAsync(x => x.VendorId == vendorId, cancellationToken);

        var json = JsonSerializer.Serialize(runtimeConfig, JsonOpts);

        if (settings == null)
        {
            settings = new VendorSettings
            {
                Id = Guid.NewGuid(),
                VendorId = vendorId,
                RuntimeConfigJson = json,
                Version = 1,
                LastModifiedUtc = DateTime.UtcNow,
                LastModifiedBy = modifiedBy
            };
            dbContext.VendorSettings.Add(settings);
        }
        else
        {
            if (settings.Version != expectedVersion)
            {
                throw new DbUpdateConcurrencyException($"Concurrency mismatch for vendor {vendorId}. Expected version: {expectedVersion}, actual version: {settings.Version}");
            }

            settings.RuntimeConfigJson = json;
            settings.Version += 1;
            settings.LastModifiedUtc = DateTime.UtcNow;
            settings.LastModifiedBy = modifiedBy;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
