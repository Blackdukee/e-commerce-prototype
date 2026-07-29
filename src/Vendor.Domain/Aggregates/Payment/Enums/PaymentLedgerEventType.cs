namespace Vendor.Domain.Aggregates.Payment.Enums;

public enum PaymentLedgerEventType
{
    Intent = 1,
    Authorized = 2,
    Captured = 3,
    Refunded = 4,
    Failed = 5
}
