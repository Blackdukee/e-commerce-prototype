using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Orders;

public record ConfirmOrderPaymentCommand(Guid OrderId) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>
{
    public string IdempotencyKey => $"CONF-ORD-{OrderId}";
}
public record StartOrderProcessingCommand(Guid OrderId) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>
{
    public string IdempotencyKey => $"PROC-ORD-{OrderId}";
}
public record ShipOrderCommand(Guid OrderId, Guid ShipmentId, string? TrackingNumber) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>
{
    public string IdempotencyKey => $"SHIP-ORD-{OrderId}";
}
public record DeliverOrderCommand(Guid OrderId) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>
{
    public string IdempotencyKey => $"DELIV-ORD-{OrderId}";
}
public record CancelOrderCommand(Guid OrderId, string? Reason = null) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>
{
    public string IdempotencyKey => $"CANCEL-ORD-{OrderId}";
}
public record RequestOrderRefundCommand(Guid OrderId, string? Reason = null) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>
{
    public string IdempotencyKey => $"REQ-REF-{OrderId}";
}
public record CompleteOrderRefundCommand(Guid OrderId) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>
{
    public string IdempotencyKey => $"COMP-REF-{OrderId}";
}

public record GetOrderByIdQuery(Guid OrderId) : IQuery<Result<OrderDto>>;
public record GetOrderByNumberQuery(string OrderNumber) : IQuery<Result<OrderDto>>;
public record GetOrdersByCustomerIdQuery(Guid CustomerId, int PageIndex = 0, int PageSize = 20) : IQuery<Result<IReadOnlyList<OrderDto>>>;
public record SearchOrdersQuery(string? Status, Guid? CustomerId, DateTime? FromDate, DateTime? ToDate, int PageIndex = 0, int PageSize = 20) : IQuery<Result<IReadOnlyList<OrderDto>>>;

public class GetOrderByIdQueryHandler(IOrderRepository orderRepository) : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(new OrderId(request.OrderId), ct);
        if (order == null) return Error.NotFound("Order", request.OrderId);
        return OrderDto.FromDomain(order);
    }
}
