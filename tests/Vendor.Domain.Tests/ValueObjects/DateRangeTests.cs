using FluentAssertions;
using Vendor.Domain.Exceptions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.ValueObjects;

public class DateRangeTests
{
    [Fact]
    public void DateRange_ValidRange_Succeeds()
    {
        var start = DateTime.UtcNow;
        var end = start.AddDays(7);

        var range = new DateRange(start, end);

        range.StartUtc.Should().Be(start);
        range.EndUtc.Should().Be(end);
    }

    [Fact]
    public void DateRange_EndBeforeStart_ThrowsBusinessRuleViolationException()
    {
        var start = DateTime.UtcNow;
        var end = start.AddDays(-1);

        Action act = () => _ = new DateRange(start, end);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void DateRange_Contains_EvaluatesBoundaries()
    {
        var start = DateTime.UtcNow.AddDays(-5);
        var end = DateTime.UtcNow.AddDays(5);
        var range = new DateRange(start, end);

        range.Contains(DateTime.UtcNow).Should().BeTrue();
        range.Contains(DateTime.UtcNow.AddDays(-10)).Should().BeFalse();
        range.Contains(DateTime.UtcNow.AddDays(10)).Should().BeFalse();
    }
}
