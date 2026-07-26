using FluentAssertions;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.ValueObjects;

public class ValueObjectTests
{
    [Fact]
    public void Money_SameCurrency_AddsCorrectly()
    {
        var m1 = new Money(10.50m, "USD");
        var m2 = new Money(5.25m, "USD");

        var sum = m1 + m2;

        sum.Amount.Should().Be(15.75m);
        sum.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_DifferentCurrency_ThrowsCurrencyMismatchException()
    {
        var m1 = new Money(10m, "USD");
        var m2 = new Money(5m, "EUR");

        Action act = () => _ = m1 + m2;

        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Slug_ValidValue_CreatesInstance()
    {
        var slug = new Slug("my-valid-product-123");

        slug.Value.Should().Be("my-valid-product-123");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("special@char")]
    public void Slug_InvalidValue_ThrowsArgumentException(string invalidSlug)
    {
        Action act = () => _ = new Slug(invalidSlug);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DateRange_ValidRange_CreatedAndChecksContains()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var range = new DateRange(start, end);

        var mid = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        range.Contains(mid).Should().BeTrue();
    }

    [Fact]
    public void DateRange_InvalidEndBeforeStart_ThrowsException()
    {
        var start = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Action act = () => _ = new DateRange(start, end);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
