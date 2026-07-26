using FluentAssertions;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Money_Addition_SameCurrency_Succeeds()
    {
        var m1 = new Money(10.50m, "USD");
        var m2 = new Money(5.25m, "USD");

        var sum = m1 + m2;

        sum.Amount.Should().Be(15.75m);
        sum.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_Addition_DifferentCurrency_ThrowsCurrencyMismatchException()
    {
        var m1 = new Money(10m, "USD");
        var m2 = new Money(10m, "EUR");

        Action act = () => _ = m1 + m2;

        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Money_Subtraction_SameCurrency_Succeeds()
    {
        var m1 = new Money(20m, "USD");
        var m2 = new Money(5m, "USD");

        var diff = m1 - m2;

        diff.Amount.Should().Be(15m);
    }

    [Fact]
    public void Money_NegativeAmount_AllowsNegativeValue()
    {
        var m = new Money(-5m, "USD");

        m.Amount.Should().Be(-5m);
        m.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_Equality_SameAmountAndCurrency_AreEqual()
    {
        var m1 = new Money(100m, "USD");
        var m2 = new Money(100m, "USD");

        m1.Should().Be(m2);
    }
}
