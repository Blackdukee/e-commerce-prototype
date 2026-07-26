using FluentAssertions;
using Vendor.Domain.ValueObjects;

namespace Vendor.Domain.Tests.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Address_ValidConstruction_Succeeds()
    {
        var address = new Address("123 Main St", "Springfield", "IL", "62701", "US");

        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Springfield");
        address.State.Should().Be("IL");
        address.ZipCode.Should().Be("62701");
        address.CountryCode.Should().Be("US");
    }

    [Theory]
    [InlineData("", "City", "State", "Zip", "US")]
    [InlineData("Street", "", "State", "Zip", "US")]
    [InlineData("Street", "City", "State", "", "US")]
    [InlineData("Street", "City", "State", "Zip", "")]
    public void Address_InvalidConstruction_ThrowsArgumentException(string street, string city, string state, string zip, string country)
    {
        Action act = () => _ = new Address(street, city, state, zip, country);

        act.Should().Throw<ArgumentException>();
    }
}
