using FluentAssertions;
using Moq;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Shipping;
using Xunit;

namespace Vendor.Infrastructure.Tests.Shipping;

public class HybridShippingProviderTests
{
    private static readonly Address Origin = new("123 Main St", "New York", "NY", "10001", "US");
    private static readonly Address Dest = new("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

    [Fact]
    public async Task WhenShippoNotConfigured_UsesFlatRate()
    {
        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), shippoProvider: null);
        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));
        rates.Should().HaveCount(1);
        rates[0].ServiceCode.Should().Be("FLAT");
    }

    [Fact]
    public async Task WhenShippoConfigured_DelegatesToShippo()
    {
        IReadOnlyList<ShippingRate> shippoRates =
            [new ShippingRate("USPS_P", "USPS Priority", new Money(7.50m, "USD"), TimeSpan.FromDays(2))];
        var mockShippo = new Mock<IShippingProvider>();
        mockShippo.Setup(s => s.GetRatesAsync(
                It.IsAny<Address>(), It.IsAny<Address>(),
                It.IsAny<Weight>(), It.IsAny<Dimensions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shippoRates);

        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), mockShippo.Object);
        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));

        rates[0].ServiceCode.Should().Be("USPS_P");
    }

    [Fact]
    public async Task WhenShippoThrows_FallsBackToFlatRate()
    {
        var mockShippo = new Mock<IShippingProvider>();
        mockShippo.Setup(s => s.GetRatesAsync(
                It.IsAny<Address>(), It.IsAny<Address>(),
                It.IsAny<Weight>(), It.IsAny<Dimensions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), mockShippo.Object);
        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));

        rates[0].ServiceCode.Should().Be("FLAT");
    }
}
