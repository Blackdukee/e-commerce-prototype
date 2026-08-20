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

    [Fact]
    public async Task RejectReturnRequest_Pending_Succeeds()
    {
        var returnReq = new ReturnRequest(
            ReturnRequestId.New(),
            OrderId.New(),
            CustomerId.New(),
            "Changed mind",
            [new ReturnItem(Guid.NewGuid(), ProductVariantId.New(), 1, "Wrong size")]);

        _returnRequestRepository.GetByIdAsync(returnReq.Id, Arg.Any<CancellationToken>()).Returns(returnReq);

        var handler = new RejectReturnRequestCommandHandler(_returnRequestRepository);
        var command = new RejectReturnRequestCommand(returnReq.Id.Value, "Not eligible for return after 30 days");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Rejected");
        await _returnRequestRepository.Received(1).UpdateAsync(returnReq, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetReturnById_Exists_ReturnsDto()
    {
        var returnReq = new ReturnRequest(
            ReturnRequestId.New(),
            OrderId.New(),
            CustomerId.New(),
            "Damaged",
            [new ReturnItem(Guid.NewGuid(), ProductVariantId.New(), 1, "Cracked")]);

        _returnRequestRepository.GetByIdAsync(returnReq.Id, Arg.Any<CancellationToken>()).Returns(returnReq);

        var handler = new Vendor.Application.Modules.Returns.Queries.GetReturnByIdQueryHandler(_returnRequestRepository);
        var result = await handler.Handle(new GetReturnByIdQuery(returnReq.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(returnReq.Id.Value);
    }

    [Fact]
    public async Task GetAdminReturns_Paged_ReturnsPagedResult()
    {
        var returnReq = new ReturnRequest(
            ReturnRequestId.New(),
            OrderId.New(),
            CustomerId.New(),
            "Defective",
            [new ReturnItem(Guid.NewGuid(), ProductVariantId.New(), 1, "Not working")]);

        _returnRequestRepository.GetPagedAsync(null, 0, 10, Arg.Any<CancellationToken>())
            .Returns(([returnReq], 1));

        var handler = new Vendor.Application.Modules.Returns.Queries.GetAdminReturnsQueryHandler(_returnRequestRepository);
        var result = await handler.Handle(new GetAdminReturnsQuery(null, 0, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(1);
        result.Value.TotalPages.Should().Be(1);
    }
}
