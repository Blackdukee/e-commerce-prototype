using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Customer;

namespace Vendor.Domain.Aggregates.AnalyticsEvent;

public class AnalyticsEvent : AggregateRoot<AnalyticsEventId>
{
    public CustomerId? CustomerId { get; private init; }
    public string EventType { get; private init; } = null!;
    public string Payload { get; private init; } = null!;
    public bool ConsentGrantedAtCapture { get; private init; }
    public DateTime OccurredAtUtc { get; private init; }

    private AnalyticsEvent() : base(default!)
    {
    }

    public AnalyticsEvent(
        AnalyticsEventId id,
        CustomerId? customerId,
        string eventType,
        string payload,
        bool consentGrantedAtCapture,
        DateTime? occurredAtUtc = null) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType, nameof(eventType));
        ArgumentException.ThrowIfNullOrWhiteSpace(payload, nameof(payload));

        CustomerId = customerId;
        EventType = eventType.Trim();
        Payload = payload.Trim();
        ConsentGrantedAtCapture = consentGrantedAtCapture;
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
    }

    public static AnalyticsEvent Capture(
        CustomerId? customerId,
        string eventType,
        string payload,
        bool consentGrantedAtCapture)
    {
        return new AnalyticsEvent(
            AnalyticsEventId.New(),
            customerId,
            eventType,
            payload,
            consentGrantedAtCapture);
    }
}
