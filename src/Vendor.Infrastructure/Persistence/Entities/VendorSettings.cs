using System;

namespace Vendor.Infrastructure.Persistence.Entities;

public sealed class VendorSettings
{
    public Guid Id { get; set; }
    public string VendorId { get; set; } = string.Empty;
    public string RuntimeConfigJson { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string LastModifiedBy { get; set; } = string.Empty;
}
