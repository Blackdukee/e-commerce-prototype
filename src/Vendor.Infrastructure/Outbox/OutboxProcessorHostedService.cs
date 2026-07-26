using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendor.Infrastructure.Persistence;

namespace Vendor.Infrastructure.Outbox;

public sealed class OutboxProcessorHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessorHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessNextBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in OutboxProcessorHostedService tick.");
            }
        }
    }

    public async Task ProcessNextBatchAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.RetryCount < 3)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type);
                if (eventType != null)
                {
                    var domainEvent = JsonSerializer.Deserialize(message.Content, eventType);
                    if (domainEvent != null)
                    {
                        await publisher.Publish(domainEvent, ct);
                    }
                }
                message.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.ToString();
                logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
