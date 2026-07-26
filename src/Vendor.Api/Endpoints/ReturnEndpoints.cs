using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class ReturnEndpoints
{
    public static RouteGroupBuilder MapReturnEndpoints(this RouteGroupBuilder group)
    {
        var returns = group.MapGroup("/returns")
            .WithTags("Returns")
            .RequireAuthorization();

        returns.MapPost("/", async (SubmitReturnRequest req, ISender mediator) =>
        {
            return Results.Created($"/api/v1/returns/{Guid.NewGuid()}", new ReturnRequestDto(
                Guid.NewGuid(), req.OrderId, "Submitted", req.Type, req.Items, DateTime.UtcNow
            ));
        });

        returns.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            return Results.Ok(new ReturnRequestDto(
                id, Guid.NewGuid(), "Submitted", "Return", Array.Empty<ReturnItemInputDto>(), DateTime.UtcNow
            ));
        });

        var adminReturns = group.MapGroup("/admin/returns")
            .WithTags("Admin Returns")
            .RequireAuthorization();

        adminReturns.MapGet("/", async (string? status, int? page, int? pageSize, ISender mediator) =>
        {
            return Results.Ok(new { items = Array.Empty<ReturnRequestDto>(), totalCount = 0, page = page ?? 1, pageSize = pageSize ?? 20 });
        });

        adminReturns.MapPost("/{id:guid}/approve", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminReturns.MapPost("/{id:guid}/reject", async (Guid id, RejectReturnRequest req, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminReturns.MapPost("/{id:guid}/items-received", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminReturns.MapPost("/{id:guid}/complete-return", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        adminReturns.MapPost("/{id:guid}/complete-exchange", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        return group;
    }
}
