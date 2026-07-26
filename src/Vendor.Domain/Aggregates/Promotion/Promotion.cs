using Vendor.Domain.Abstractions;
using Vendor.Domain.Events;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Aggregates.Promotion;

public enum DiscountType
{
    Percentage,
    Fixed
}

public class Promotion : AggregateRoot<PromotionId>
{
    public string Code { get; private set; }
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public Money? MaxDiscountAmount { get; private set; }
    public Money? MinOrderAmount { get; private set; }
    public DateRange Validity { get; private set; }
    public int? MaxUsageCount { get; private set; }
    public int CurrentUsageCount { get; private set; }
    public bool IsActive { get; private set; }

    private Promotion() : base(default!)
    {
        Code = null!;
        Validity = default!;
    }

    public Promotion(
        PromotionId id,
        string code,
        DiscountType discountType,
        decimal discountValue,
        DateRange validity,
        Money? maxDiscountAmount = null,
        Money? minOrderAmount = null,
        int? maxUsageCount = null) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));

        if (discountValue <= 0m)
        {
            throw new BusinessRuleViolationException("Discount value must be greater than zero.", nameof(Promotion));
        }

        if (discountType == DiscountType.Percentage && discountValue > 100m)
        {
            throw new BusinessRuleViolationException("Percentage discount cannot exceed 100%.", nameof(Promotion));
        }

        if (maxUsageCount.HasValue && maxUsageCount.Value <= 0)
        {
            throw new BusinessRuleViolationException("Max usage count must be positive if specified.", nameof(Promotion));
        }

        Code = code.Trim().ToUpperInvariant();
        DiscountType = discountType;
        DiscountValue = discountValue;
        Validity = validity;
        MaxDiscountAmount = maxDiscountAmount;
        MinOrderAmount = minOrderAmount;
        MaxUsageCount = maxUsageCount;
        CurrentUsageCount = 0;
        IsActive = true;
    }

    public bool IsValidAt(DateTime utcNow, Money orderSubtotal)
    {
        if (!IsActive) return false;
        if (!Validity.Contains(utcNow)) return false;

        if (MaxUsageCount.HasValue && CurrentUsageCount >= MaxUsageCount.Value)
        {
            return false;
        }

        if (MinOrderAmount.HasValue && orderSubtotal < MinOrderAmount.Value)
        {
            return false;
        }

        return true;
    }

    public Money CalculateDiscount(Money subtotal)
    {
        if (DiscountType == DiscountType.Percentage)
        {
            var calculated = subtotal.Amount * (DiscountValue / 100m);
            if (MaxDiscountAmount.HasValue && calculated > MaxDiscountAmount.Value.Amount)
            {
                return MaxDiscountAmount.Value;
            }
            return new Money(calculated, subtotal.Currency);
        }

        // Fixed discount
        var fixedAmount = Math.Min(DiscountValue, subtotal.Amount);
        return new Money(fixedAmount, subtotal.Currency);
    }

    public Money CalculateDiscount(Money subtotal, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new BusinessRuleViolationException("Cannot calculate discount for an inactive promotion.", nameof(Promotion));
        }
        return CalculateDiscount(subtotal);
    }

    public void RecordUsage()
    {
        CurrentUsageCount++;

        if (MaxUsageCount.HasValue && CurrentUsageCount >= MaxUsageCount.Value)
        {
            IsActive = false;
            RaiseDomainEvent(new PromotionExhaustedEvent(Id, Code, CurrentUsageCount, DateTime.UtcNow));
        }
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
