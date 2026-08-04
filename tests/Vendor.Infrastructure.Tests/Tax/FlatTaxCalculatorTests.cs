using FluentAssertions;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;
using Vendor.Infrastructure.Tax;
using Xunit;

namespace Vendor.Infrastructure.Tests.Tax;

public class FlatTaxCalculatorTests
{
    private static readonly Address ShipAddr = new("15 Tahrir Sq", "Cairo", "Cairo", "11511", "EG");

    private static OrderLine MakeLine(decimal unitPrice, int qty) =>
        new(new OrderId(Guid.NewGuid()), new ProductVariantId(Guid.NewGuid()),
            "Test Product", "SKU-EGY-001", qty, new Money(unitPrice, "EGP"));

    [Fact]
    public async Task CalculateTaxAsync_Applies14PercentEgyptianVat()
    {
        var calculator = new FlatTaxCalculator(); // Default 14% Egyptian VAT
        var lines = new List<OrderLine> { MakeLine(1000m, 1) }; // 1,000 EGP subtotal

        var tax = await calculator.CalculateTaxAsync(lines, ShipAddr, "EGP");

        tax.Amount.Should().Be(140.00m); // 1,000 * 0.14 = 140 EGP VAT
        tax.Currency.Should().Be("EGP");
    }

    [Fact]
    public async Task CalculateTaxAsync_WithMultipleLines_Applies14PercentVat()
    {
        var calculator = new FlatTaxCalculator();
        var lines = new List<OrderLine>
        {
            MakeLine(250m, 2), // 500 EGP
            MakeLine(150m, 1)  // 150 EGP -> total 650 EGP
        };

        var tax = await calculator.CalculateTaxAsync(lines, ShipAddr, "EGP");

        tax.Amount.Should().Be(91.00m); // 650 * 0.14 = 91 EGP VAT
    }
}
