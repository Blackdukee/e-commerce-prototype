using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Api.Extensions;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Orders;

namespace Vendor.Api.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this RouteGroupBuilder group)
    {
        var orders = group.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        orders.MapGet("/my-orders", async (int? page, int? pageSize, ICurrentUserService user, ISender mediator, CancellationToken ct) =>
        {
            var customerId = user.CustomerId ?? Guid.Empty;
            var pIndex = (page ?? 1) - 1;
            var pSize = Math.Min(pageSize ?? 20, 100);
            var result = await mediator.Send(new GetOrdersByCustomerIdQuery(customerId, pIndex <= 0 ? 0 : pIndex, pSize <= 0 ? 20 : pSize), ct);
            return result.ToHttpResult();
        });

        orders.MapGet("/{id:guid}", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetOrderByIdQuery(id), ct);
            return result.ToHttpResult();
        });

        orders.MapGet("/number/{orderNumber}", async (string orderNumber, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetOrderByNumberQuery(orderNumber), ct);
            return result.ToHttpResult();
        });

        orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest req, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CancelOrderCommand(id, req.Reason), ct);
            return result.ToHttpResult();
        });

        orders.MapPost("/{id:guid}/refund-request", async (Guid id, RefundRequestInputDto req, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RequestOrderRefundCommand(id, req.Reason), ct);
            return result.ToHttpResult();
        });

        var adminOrders = group.MapGroup("/admin/orders")
            .WithTags("Admin Orders")
            .RequireAuthorization();

        adminOrders.MapGet("/", async (string? status, int? page, int? pageSize, ISender mediator, CancellationToken ct) =>
        {
            var pIndex = (page ?? 1) - 1;
            var pSize = Math.Min(pageSize ?? 20, 100);
            var result = await mediator.Send(new SearchOrdersQuery(status, null, null, null, pIndex <= 0 ? 0 : pIndex, pSize <= 0 ? 20 : pSize), ct);
            return result.ToHttpResult();
        });

        adminOrders.MapPost("/{id:guid}/process", async (Guid id, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new StartOrderProcessingCommand(id), ct);
            return result.ToHttpResult();
        });

        return group;
    }
}
