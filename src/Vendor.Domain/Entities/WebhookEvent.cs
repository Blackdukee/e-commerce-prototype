namespace Vendor.Domain.Entities;

public class WebhookEvent
{
    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string EventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; private set; }

    private WebhookEvent() { }

    public WebhookEvent(Guid id, string provider, string eventId, string eventType, string payloadJson)
    {
        Id = id;
        Provider = provider;
        EventId = eventId;
        EventType = eventType;
        PayloadJson = payloadJson;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
