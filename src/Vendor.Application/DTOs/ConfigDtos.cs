using System;
using Vendor.Domain.Aggregates.VendorSettings;

namespace Vendor.Application.DTOs;

public sealed record VendorConfigDto(
    string VendorId,
    string VendorDisplayName,
    VendorTiersDto Tiers,
    int Version,
    DateTime LastModifiedUtc);

public sealed record VendorTiersDto(
    VendorBuildConfig Build,
    VendorBootConfig Boot,
    VendorRuntimeConfig Runtime);

public sealed record VendorConfigPatchDto(
    VendorRuntimeConfig? Runtime,
    int Version);
