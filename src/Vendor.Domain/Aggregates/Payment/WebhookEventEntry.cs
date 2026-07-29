using Vendor.Domain.Abstractions;

namespace Vendor.Domain.Aggregates.Payment;

public class WebhookEventEntry : Entity<Guid>
{
    public string GatewayName { get; private set; }
    public string EventId { get; private set; }
    public string EventType { get; private set; }
    public string PayloadHash { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public bool IsProcessed { get; private set; }

    private WebhookEventEntry()
    {
        GatewayName = null!;
        EventId = null!;
        EventType = null!;
        PayloadHash = null!;
    }

    public WebhookEventEntry(string gatewayName, string eventId, string eventType, string payloadHash)
        : base(Guid.NewGuid())
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayName, nameof(gatewayName));
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId, nameof(eventId));
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType, nameof(eventType));
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash, nameof(payloadHash));

        GatewayName = gatewayName.Trim();
        EventId = eventId.Trim();
        EventType = eventType.Trim();
        PayloadHash = payloadHash.Trim();
        ReceivedAtUtc = DateTime.UtcNow;
        IsProcessed = false;
    }

    public void MarkProcessed()
    {
        IsProcessed = true;
    }
}
