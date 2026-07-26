using FluentAssertions;
using Vendor.Domain.Aggregates.AnalyticsEvent;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Infrastructure.Analytics;

namespace Vendor.Infrastructure.Tests.Analytics;

public class AnalyticsProcessorTests
{
    [Fact]
    public void AnalyticsChannel_ConsentGranted_EnqueuesSuccessfully()
    {
        var channel = new AnalyticsChannel();
        var evt = AnalyticsEvent.Capture(CustomerId.New(), "PageView", "{}", true);

        var enqueued = channel.TryEnqueue(evt);
        enqueued.Should().BeTrue();
    }

    [Fact]
    public void AnalyticsChannel_ConsentDenied_DiscardsEvent()
    {
        var channel = new AnalyticsChannel();
        var evt = AnalyticsEvent.Capture(CustomerId.New(), "PageView", "{}", false);

        var enqueued = channel.TryEnqueue(evt);
        enqueued.Should().BeFalse();
    }
}
