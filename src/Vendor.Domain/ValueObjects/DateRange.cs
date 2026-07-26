using Vendor.Domain.Exceptions;

namespace Vendor.Domain.ValueObjects;

public readonly record struct DateRange
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public DateRange(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc < startUtc)
        {
            throw new BusinessRuleViolationException(
                "EndUtc must be greater than or equal to StartUtc.",
                nameof(DateRange));
        }

        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public bool Contains(DateTime utcNow)
    {
        return utcNow >= StartUtc && utcNow <= EndUtc;
    }

    public bool IsActive(DateTime utcNow) => Contains(utcNow);
}
