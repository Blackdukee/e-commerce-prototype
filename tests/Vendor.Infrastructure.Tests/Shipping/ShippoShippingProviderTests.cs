using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Shipping;
using Xunit;

namespace Vendor.Infrastructure.Tests.Shipping;

public class ShippoShippingProviderTests
{
    private static HttpClient CreateMockedClient(HttpStatusCode status, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.goshippo.com/") };
    }

    [Fact]
    public async Task GetRatesAsync_OnApiSuccess_ReturnsRates()
    {
        var json = """
        {
          "rates": [{
            "servicelevel": { "token": "usps_priority", "name": "USPS Priority Mail" },
            "amount": "7.50", "currency": "USD", "estimated_days": 2, "object_id": "rate-123"
          }]
        }
        """;
        var client = CreateMockedClient(HttpStatusCode.OK, json);
        var svc = new ShippoShippingProvider(client, "test-key");
        var origin = new Address("123 Main St", "New York", "NY", "10001", "US");
        var dest = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var rates = await svc.GetRatesAsync(origin, dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));

        rates.Should().HaveCount(1);
        rates[0].ServiceCode.Should().Be("usps_priority");
        rates[0].Cost.Amount.Should().Be(7.50m);
        rates[0].Cost.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task GetRatesAsync_OnApiFailure_ThrowsHttpRequestException()
    {
        var client = CreateMockedClient(HttpStatusCode.Unauthorized, "{}");
        var svc = new ShippoShippingProvider(client, "bad-key");
        var origin = new Address("123 Main St", "New York", "NY", "10001", "US");
        var dest = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var act = async () => await svc.GetRatesAsync(origin, dest, new Weight(0.5m, WeightUnit.Kg), new Dimensions(10m, 10m, 10m, DimensionUnit.Cm));

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
