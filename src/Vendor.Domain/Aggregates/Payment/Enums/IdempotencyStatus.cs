namespace Vendor.Domain.Aggregates.Payment.Enums;

public enum IdempotencyStatus
{
    Processing = 0,
    Completed = 1,
    Failed = 2
}
