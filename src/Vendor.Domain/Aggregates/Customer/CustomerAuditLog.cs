namespace Vendor.Domain.Aggregates.Customer;

public class CustomerAuditLog
{
    public Guid Id { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public string EventType { get; private set; }
    public string DetailsJson { get; private set; }
    public CustomerId PerformedByCustomerId { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    private CustomerAuditLog()
    {
        Id = Guid.NewGuid();
        CustomerId = default!;
        EventType = null!;
        DetailsJson = null!;
        PerformedByCustomerId = default!;
        TimestampUtc = DateTime.UtcNow;
    }

    public CustomerAuditLog(
        Guid id,
        CustomerId customerId,
        string eventType,
        string detailsJson,
        CustomerId performedByCustomerId,
        DateTime timestampUtc)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        EventType = eventType;
        DetailsJson = detailsJson;
        PerformedByCustomerId = performedByCustomerId;
        TimestampUtc = timestampUtc;
    }
}
