using FluentAssertions;
using Moq;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Tests.Handlers;

public class PlaceOrderHandlerTests
{
    [Fact]
    public void OrderPlacement_WithValidData_ReturnsSuccessResult()
    {
        var customerId = CustomerId.New();
        var orderId = OrderId.New();
        var address = new Address("123 Main St", "Springfield", "IL", "62701", "US");
        var line = new OrderLine(orderId, ProductVariantId.New(), "Product 1", "SKU1", 2, new Money(50m, "USD"));

        var order = new Order(
            orderId,
            customerId,
            "ORD-20260725-001",
            address,
            [line],
            tax: new Money(10m, "USD"),
            shippingCost: new Money(5m, "USD"),
            discount: new Money(0m, "USD"));

        var orderRepoMock = new Mock<IOrderRepository>();
        orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        order.Status.Should().Be(OrderStatus.Pending);
        order.Total.Amount.Should().Be(115m);
        order.CustomerId.Should().Be(customerId);
    }
}
