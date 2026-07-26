using System;
using System.Threading;
using System.Threading.Tasks;
using Vendor.Domain.Aggregates.VendorSettings;

namespace Vendor.Domain.Interfaces;

public interface IVendorSettingsRepository
{
    Task<VendorRuntimeConfig?> GetRuntimeConfigAsync(string vendorId, CancellationToken cancellationToken = default);
    Task<int> GetVersionAsync(string vendorId, CancellationToken cancellationToken = default);
    Task UpdateRuntimeConfigAsync(string vendorId, VendorRuntimeConfig runtimeConfig, int expectedVersion, string modifiedBy, CancellationToken cancellationToken = default);
}
