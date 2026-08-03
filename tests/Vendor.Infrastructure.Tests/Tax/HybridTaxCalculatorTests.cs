using FluentAssertions;
using Moq;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Tax;
using Xunit;

namespace Vendor.Infrastructure.Tests.Tax;

public class HybridTaxCalculatorTests
{
    private static readonly Address ShipAddr = new("456 Oak Ave", "Los Angeles", "CA", "90001", "US");

    private static OrderLine MakeLine(decimal unitPrice, int qty) =>
        new(new OrderId(Guid.NewGuid()), new ProductVariantId(Guid.NewGuid()),
            "Test Product", "SKU-001", qty, new Money(unitPrice, "USD"));

    [Fact]
    public async Task WhenTaxJarNotConfigured_UsesFlatRate()
    {
        var hybrid = new HybridTaxCalculator(new FlatTaxCalculator(), taxJarCalculator: null);
        var lines = new List<OrderLine> { MakeLine(100m, 1) };

        var tax = await hybrid.CalculateTaxAsync(lines, ShipAddr, "USD");

        tax.Amount.Should().Be(Math.Round(100m * 0.08875m, 2));
    }

    [Fact]
    public async Task WhenTaxJarConfigured_DelegatesToIt()
    {
        var mockTj = new Mock<ITaxCalculator>();
        mockTj.Setup(s => s.CalculateTaxAsync(
                It.IsAny<IReadOnlyList<OrderLine>>(), It.IsAny<Address>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new Money(9.99m, "USD"));

        var hybrid = new HybridTaxCalculator(new FlatTaxCalculator(), mockTj.Object);
        var lines = new List<OrderLine> { MakeLine(100m, 1) };

        var tax = await hybrid.CalculateTaxAsync(lines, ShipAddr, "USD");

        tax.Amount.Should().Be(9.99m);
    }

    [Fact]
    public async Task WhenTaxJarThrows_FallsBackToFlatRate()
    {
        var mockTj = new Mock<ITaxCalculator>();
        mockTj.Setup(s => s.CalculateTaxAsync(
                It.IsAny<IReadOnlyList<OrderLine>>(), It.IsAny<Address>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new HttpRequestException("TaxJar offline"));

        var hybrid = new HybridTaxCalculator(new FlatTaxCalculator(), mockTj.Object);
        var lines = new List<OrderLine> { MakeLine(100m, 1) };

        var tax = await hybrid.CalculateTaxAsync(lines, ShipAddr, "USD");

        tax.Amount.Should().Be(Math.Round(100m * 0.08875m, 2));
    }
}
