using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Shipping;
using Xunit;

namespace Vendor.Infrastructure.Tests.Shipping;

public class HybridShippingProviderTests
{
    private static readonly Address Origin = new("123 Main St", "Cairo", "Cairo", "11511", "EG");
    private static readonly Address Dest = new("456 Tahrir", "Cairo", "Cairo", "11511", "EG");

    [Fact]
    public async Task WhenBostaNotConfigured_UsesFlatRate()
    {
        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), bostaProvider: null);
        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));
        rates.Should().HaveCount(1);
        rates[0].ServiceCode.Should().Be("FLAT");
    }

    [Fact]
    public async Task WhenBostaConfigured_UsesBostaRates()
    {
        var mockBosta = new Mock<IShippingProvider>();
        IReadOnlyList<ShippingRate> bostaRates = [new ShippingRate("BOSTA_STD", "Bosta Standard", new Money(50m, "EGP"), TimeSpan.FromDays(1))];

        mockBosta
            .Setup(s => s.GetRatesAsync(It.IsAny<Address>(), It.IsAny<Address>(), It.IsAny<Weight>(), It.IsAny<Dimensions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bostaRates);

        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), mockBosta.Object);
        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));

        rates[0].ServiceCode.Should().Be("BOSTA_STD");
    }

    [Fact]
    public async Task WhenBostaFails_FallsBackToFlatRate()
    {
        var mockBosta = new Mock<IShippingProvider>();
        mockBosta
            .Setup(s => s.GetRatesAsync(It.IsAny<Address>(), It.IsAny<Address>(), It.IsAny<Weight>(), It.IsAny<Dimensions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var hybrid = new HybridShippingProvider(new FlatRateShippingProvider(), mockBosta.Object);
        var rates = await hybrid.GetRatesAsync(Origin, Dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));

        rates[0].ServiceCode.Should().Be("FLAT");
    }
}
