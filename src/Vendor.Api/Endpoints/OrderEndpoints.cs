using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this RouteGroupBuilder group)
    {
        var orders = group.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        orders.MapGet("/my-orders", async (int? page, int? pageSize, ISender mediator) =>
        {
            return Results.Ok(new OrderListResponse(Array.Empty<OrderSummaryDto>(), 0, page ?? 1, pageSize ?? 20));
        });

        orders.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            return Results.Ok(new OrderDto(
                id, "ORD-1001", "Placed", Array.Empty<OrderLineDto>(),
                new AddressDto("123 Main St", "NYC", "NY", "10001", "US"),
                new MoneyDto(100m, "USD"), new MoneyDto(8m, "USD"), new MoneyDto(5m, "USD"), new MoneyDto(0m, "USD"), new MoneyDto(113m, "USD"),
                DateTime.UtcNow
            ));
        });

        orders.MapGet("/number/{orderNumber}", async (string orderNumber, ISender mediator) =>
        {
            return Results.Ok(new OrderDto(
                Guid.NewGuid(), orderNumber, "Placed", Array.Empty<OrderLineDto>(),
                new AddressDto("123 Main St", "NYC", "NY", "10001", "US"),
                new MoneyDto(100m, "USD"), new MoneyDto(8m, "USD"), new MoneyDto(5m, "USD"), new MoneyDto(0m, "USD"), new MoneyDto(113m, "USD"),
                DateTime.UtcNow
            ));
        });

        orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest req, ISender mediator) =>
        {
            return Results.NoContent();
        });

        orders.MapPost("/{id:guid}/refund-request", async (Guid id, RefundRequestInputDto req, ISender mediator) =>
        {
            return Results.Accepted();
        });

        var adminOrders = group.MapGroup("/admin/orders")
            .WithTags("Admin Orders")
            .RequireAuthorization();

        adminOrders.MapGet("/", async (string? status, int? page, int? pageSize, ISender mediator) =>
        {
            return Results.Ok(new OrderListResponse(Array.Empty<OrderSummaryDto>(), 0, page ?? 1, pageSize ?? 20));
        });

        adminOrders.MapPost("/{id:guid}/process", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminOrders.MapPost("/{id:guid}/notes", async (Guid id, AddOrderNoteRequest req, ISender mediator) =>
        {
            return Results.NoContent();
        });

        return group;
    }

    private record OrderListResponse(OrderSummaryDto[] Items, int TotalCount, int Page, int PageSize);
    private record OrderSummaryDto(Guid Id, string OrderNumber, string Status, MoneyDto Total, DateTime PlacedAtUtc);
}
