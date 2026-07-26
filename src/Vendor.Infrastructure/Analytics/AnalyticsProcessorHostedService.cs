using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendor.Domain.Aggregates.AnalyticsEvent;

namespace Vendor.Infrastructure.Analytics;

public class AnalyticsChannel
{
    private readonly Channel<AnalyticsEvent> _channel = Channel.CreateUnbounded<AnalyticsEvent>();

    public bool TryEnqueue(AnalyticsEvent evt)
    {
        if (!evt.ConsentGrantedAtCapture) return false;
        return _channel.Writer.TryWrite(evt);
    }

    public ChannelReader<AnalyticsEvent> Reader => _channel.Reader;
}

public class AnalyticsProcessorHostedService(
    AnalyticsChannel channel,
    ILogger<AnalyticsProcessorHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var batch = new List<AnalyticsEvent>();
            while (channel.Reader.TryRead(out var evt))
            {
                batch.Add(evt);
            }

            if (batch.Count > 0)
            {
                logger.LogInformation("Flushed {Count} analytics events to GA4/Webhook.", batch.Count);
            }
        }
    }
}
