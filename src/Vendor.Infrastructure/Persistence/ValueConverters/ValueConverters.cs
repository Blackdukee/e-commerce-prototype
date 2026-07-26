using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Persistence.ValueConverters;

public class WeightConverter() : ValueConverter<Weight, string>(
    w => $"{w.Value.ToString(CultureInfo.InvariantCulture)}:{w.Unit}",
    s => ParseWeight(s))
{
    private static Weight ParseWeight(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new Weight(0m, WeightUnit.Kg);
        var parts = s.Split(':');
        var val = decimal.Parse(parts[0], CultureInfo.InvariantCulture);
        var unit = Enum.Parse<WeightUnit>(parts[1]);
        return new Weight(val, unit);
    }
}

public class DimensionsConverter() : ValueConverter<Dimensions, string>(
    d => $"{d.Length.ToString(CultureInfo.InvariantCulture)}:{d.Width.ToString(CultureInfo.InvariantCulture)}:{d.Height.ToString(CultureInfo.InvariantCulture)}:{d.Unit}",
    s => ParseDimensions(s))
{
    private static Dimensions ParseDimensions(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new Dimensions(0m, 0m, 0m, DimensionUnit.Cm);
        var parts = s.Split(':');
        var l = decimal.Parse(parts[0], CultureInfo.InvariantCulture);
        var w = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
        var h = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
        var unit = Enum.Parse<DimensionUnit>(parts[3]);
        return new Dimensions(l, w, h, unit);
    }
}
