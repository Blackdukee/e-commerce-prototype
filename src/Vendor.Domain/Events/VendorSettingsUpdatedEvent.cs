using System;

namespace Vendor.Domain.Events;

public sealed record VendorSettingsUpdatedEvent(
    string VendorId,
    int Version,
    DateTime OccurredOnUtc,
    string ModifiedBy);
