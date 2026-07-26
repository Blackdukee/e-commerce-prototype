using FluentAssertions;
using NSubstitute;
using Vendor.Application.Modules.Returns.Commands;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Tests.Modules;

public class ReturnWorkflowTests
{
    private readonly IReturnRequestRepository _returnRequestRepository = Substitute.For<IReturnRequestRepository>();
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();

    [Fact]
    public async Task SubmitReturnRequest_DeliveredOrder_Succeeds()
    {
        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var address = new Address("123 St", "City", "ST", "12345", "US");
        var line = new OrderLine(orderId, ProductVariantId.New(), "Item", "SKU1", 1, new Money(100m, "USD"));
        var order = new Order(orderId, customerId, "ORD-001", address, [line], Money.Zero("USD"), Money.Zero("USD"), Money.Zero("USD"));
        order.ConfirmPayment();
        order.StartProcessing();
        order.Ship(Vendor.Domain.Aggregates.Shipment.ShipmentId.New());
        order.Deliver();

        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(order);

        var handler = new SubmitReturnRequestCommandHandler(_returnRequestRepository, _orderRepository);
        var command = new SubmitReturnRequestCommand(orderId.Value, customerId.Value, "Defective", [new ReturnItemInputDto(Guid.NewGuid(), ProductVariantId.New().Value, 1, "Defective")]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
        await _returnRequestRepository.Received(1).AddAsync(Arg.Any<ReturnRequest>(), Arg.Any<CancellationToken>());
    }
}
