namespace Vendor.Infrastructure.Outbox;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processed = 1,
    DeadLetter = 2,
    Failed = 3
}

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredOnUtc { get; set; }
    public DateTime CreatedAtUtc
    {
        get => OccurredOnUtc;
        set => OccurredOnUtc = value;
    }

    public DateTime? ProcessedOnUtc { get; set; }
    public DateTime? ProcessedAtUtc
    {
        get => ProcessedOnUtc;
        set => ProcessedOnUtc = value;
    }

    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string content, DateTime occurredOnUtc)
    {
        Id = id;
        Type = type;
        Content = content;
        OccurredOnUtc = occurredOnUtc;
        Status = OutboxMessageStatus.Pending;
        RetryCount = 0;
    }

    public void MarkAsProcessed()
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsFailed(string error)
    {
        RetryCount++;
        Error = error;
        if (RetryCount >= 5)
        {
            Status = OutboxMessageStatus.DeadLetter;
        }
    }
}
