using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Tax;
using Xunit;

namespace Vendor.Infrastructure.Tests.Tax;

public class TaxJarTaxCalculatorTests
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
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.taxjar.com/v2/") };
    }

    private static OrderLine MakeLine(decimal unitPrice, int qty) =>
        new(new OrderId(Guid.NewGuid()), new ProductVariantId(Guid.NewGuid()),
            "Test Product", "SKU-001", qty, new Money(unitPrice, "USD"));

    [Fact]
    public async Task CalculateTaxAsync_OnSuccess_ReturnsTaxAmount()
    {
        var json = """{"tax": {"amount_to_collect": 8.88}}""";
        var client = CreateMockedClient(HttpStatusCode.OK, json);
        var svc = new TaxJarTaxCalculator(client, "test-key");
        var lines = new List<OrderLine> { MakeLine(100m, 1) };
        var address = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var tax = await svc.CalculateTaxAsync(lines, address, "USD");

        tax.Amount.Should().Be(8.88m);
        tax.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task CalculateTaxAsync_OnApiFailure_ThrowsHttpRequestException()
    {
        var client = CreateMockedClient(HttpStatusCode.Unauthorized, "{}");
        var svc = new TaxJarTaxCalculator(client, "bad-key");
        var lines = new List<OrderLine> { MakeLine(100m, 1) };
        var address = new Address("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

        var act = async () => await svc.CalculateTaxAsync(lines, address, "USD");
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
