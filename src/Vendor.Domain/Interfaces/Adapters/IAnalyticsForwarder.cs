using Vendor.Domain.Aggregates.AnalyticsEvent;

namespace Vendor.Domain.Interfaces.Adapters;

public interface IAnalyticsForwarder
{
    Task ForwardAsync(AnalyticsEvent analyticsEvent, CancellationToken ct = default);
}
