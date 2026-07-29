using Vendor.Domain.Abstractions;
using Vendor.Domain.Aggregates.Payment.Enums;

namespace Vendor.Domain.Aggregates.Payment;

public class PaymentIdempotencyKey : Entity<Guid>
{
    public Guid KeyUuid { get; private set; }
    public string RequestHash { get; private set; }
    public IdempotencyStatus Status { get; private set; }
    public int? ResponseStatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    private PaymentIdempotencyKey()
    {
        RequestHash = null!;
    }

    public PaymentIdempotencyKey(Guid keyUuid, string requestHash, TimeSpan? retentionWindow = null)
        : base(Guid.NewGuid())
    {
        if (keyUuid == Guid.Empty)
        {
            throw new ArgumentException("Idempotency key UUID cannot be empty.", nameof(keyUuid));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash, nameof(requestHash));

        KeyUuid = keyUuid;
        RequestHash = requestHash.Trim();
        Status = IdempotencyStatus.Processing;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(retentionWindow ?? TimeSpan.FromHours(24));
    }

    public bool MatchesHash(string requestHash)
    {
        if (string.IsNullOrWhiteSpace(requestHash)) return false;
        return string.Equals(RequestHash, requestHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public void MarkCompleted(int statusCode, string responseBody)
    {
        Status = IdempotencyStatus.Completed;
        ResponseStatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public void MarkFailed(int statusCode, string responseBody)
    {
        Status = IdempotencyStatus.Failed;
        ResponseStatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
