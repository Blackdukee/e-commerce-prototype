using System.Net;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Shipping;
using Xunit;

namespace Vendor.Infrastructure.Tests.Shipping;

public class BostaShippingProviderTests
{
    private static HttpClient CreateMockedClient(HttpStatusCode status, string json)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(json)
            });

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.staging.bosta.co/v2/")
        };
        return client;
    }

    [Fact]
    public async Task GetRatesAsync_CairoDestination_ReturnsCairoRateInEgp()
    {
        var client = CreateMockedClient(HttpStatusCode.OK, "{}");
        var svc = new BostaShippingProvider(client, "test-bosta-key");

        var origin = new Address("123 Main St", "Cairo", "Cairo", "11511", "EG");
        var dest = new Address("456 Tahrir", "Cairo", "Cairo", "11511", "EG");

        var rates = await svc.GetRatesAsync(origin, dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));

        rates.Should().HaveCount(2);
        rates[0].ServiceCode.Should().Be("BOSTA_STANDARD");
        rates[0].Cost.Amount.Should().Be(50.00m);
        rates[0].Cost.Currency.Should().Be("EGP");
    }

    [Fact]
    public async Task CreateLabelAsync_MockClient_ReturnsBostaLabel()
    {
        var client = CreateMockedClient(HttpStatusCode.OK, "{\"data\":{\"trackingNumber\":\"BOSTA-12345\"}}");
        var svc = new BostaShippingProvider(client, "test-bosta-key");

        var origin = new Address("123 Main St", "Cairo", "Cairo", "11511", "EG");
        var dest = new Address("456 Tahrir", "Cairo", "Cairo", "11511", "EG");
        var rate = new ShippingRate("BOSTA_STANDARD", "Standard", new Money(50m, "EGP"), TimeSpan.FromDays(1));

        var label = await svc.CreateLabelAsync(rate, origin, dest);

        label.CarrierCode.Should().Be("BOSTA");
        label.TrackingNumber.Should().Be("BOSTA-12345");
    }
}
