using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vendor.Domain.ValueObjects;

namespace Vendor.Infrastructure.Persistence.ValueConverters;

public class MoneyConverter() : ValueConverter<Money, string>(
    m => $"{m.Amount.ToString(CultureInfo.InvariantCulture)}:{m.Currency}",
    s => ParseMoney(s))
{
    private static Money ParseMoney(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new Money(0m, "USD");
        var parts = s.Split(':');
        if (parts.Length < 2) return new Money(0m, "USD");
        var amount = decimal.Parse(parts[0], CultureInfo.InvariantCulture);
        return new Money(amount, parts[1]);
    }
}

public class NullableMoneyConverter() : ValueConverter<Money?, string?>(
    m => m.HasValue ? $"{m.Value.Amount.ToString(CultureInfo.InvariantCulture)}:{m.Value.Currency}" : null,
    s => ParseNullableMoney(s))
{
    private static Money? ParseNullableMoney(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Split(':');
        if (parts.Length < 2) return null;
        var amount = decimal.Parse(parts[0], CultureInfo.InvariantCulture);
        return new Money(amount, parts[1]);
    }
}
