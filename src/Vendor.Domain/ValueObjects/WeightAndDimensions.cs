using Vendor.Domain.Exceptions;

namespace Vendor.Domain.ValueObjects;

public enum WeightUnit
{
    Kg,
    Lb
}

public enum DimensionUnit
{
    Cm,
    In
}

public readonly record struct Weight
{
    public decimal Value { get; }
    public WeightUnit Unit { get; }

    public Weight(decimal value, WeightUnit unit)
    {
        if (value <= 0m)
        {
            throw new BusinessRuleViolationException("Weight value must be greater than zero.", nameof(Weight));
        }

        Value = value;
        Unit = unit;
    }
}

public readonly record struct Dimensions
{
    public decimal Length { get; }
    public decimal Width { get; }
    public decimal Height { get; }
    public DimensionUnit Unit { get; }

    public Dimensions(decimal length, decimal width, decimal height, DimensionUnit unit)
    {
        if (length <= 0m || width <= 0m || height <= 0m)
        {
            throw new BusinessRuleViolationException("Dimensions must all be greater than zero.", nameof(Dimensions));
        }

        Length = length;
        Width = width;
        Height = height;
        Unit = unit;
    }
}
