using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Api.Extensions;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.ReturnRequest;

namespace Vendor.Api.Endpoints;

public static class ReturnEndpoints
{
    public static RouteGroupBuilder MapReturnEndpoints(this RouteGroupBuilder group)
    {
        var returns = group.MapGroup("/returns")
            .WithTags("Returns")
            .RequireAuthorization();

        returns.MapPost("/", async (SubmitReturnRequest req, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var customerIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(customerIdClaim, out var customerId))
            {
                customerId = Guid.NewGuid();
            }

            var items = req.Items?.Select(i => new Vendor.Application.Modules.Returns.Dtos.ReturnItemInputDto(
                i.OrderLineId,
                i.ExchangeVariantId ?? Guid.Empty,
                i.Quantity,
                req.Reason ?? "Customer Return")).ToList() ?? [];

            var resolution = string.Equals(req.Type, "Exchange", StringComparison.OrdinalIgnoreCase)
                ? ResolutionType.Exchange
                : ResolutionType.Refund;

            var command = new SubmitReturnRequestCommand(req.OrderId, customerId, req.Reason ?? "Customer Return", items, resolution);
            var result = await mediator.Send(command, ct);
            return result.ToCreatedHttpResult($"/api/v1/returns/{result.Value?.Id}", context);
        });

        returns.MapGet("/{id:guid}", async (Guid id, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetReturnByIdQuery(id), ct);
            return result.ToHttpResult(context);
        });

        var adminReturns = group.MapGroup("/admin/returns")
            .WithTags("Admin Returns")
            .RequireAuthorization();

        adminReturns.MapGet("/", async (string? status, int? page, int? pageSize, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var pIndex = (page ?? 1) - 1;
            var pSize = Math.Min(pageSize ?? 20, 100);
            var result = await mediator.Send(new GetAdminReturnsQuery(status, pIndex <= 0 ? 0 : pIndex, pSize <= 0 ? 20 : pSize), ct);
            return result.ToHttpResult(context);
        });

        adminReturns.MapPost("/{id:guid}/approve", async (Guid id, ApproveReturnRequestDto? body, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var res = body?.Resolution != null && Enum.TryParse<ResolutionType>(body.Resolution, true, out var parsedRes)
                ? parsedRes
                : ResolutionType.Refund;
            var result = await mediator.Send(new ApproveReturnRequestCommand(id, res), ct);
            return result.ToHttpResult(context);
        });

        adminReturns.MapPost("/{id:guid}/reject", async (Guid id, RejectReturnRequest req, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RejectReturnRequestCommand(id, req.Reason ?? "Rejected by admin"), ct);
            return result.ToHttpResult(context);
        });

        adminReturns.MapPost("/{id:guid}/items-received", async (Guid id, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new MarkReturnItemsReceivedCommand(id), ct);
            return result.ToHttpResult(context);
        });

        adminReturns.MapPost("/{id:guid}/complete-return", async (Guid id, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CompleteReturnRefundCommand(id), ct);
            return result.ToHttpResult(context);
        });

        adminReturns.MapPost("/{id:guid}/complete-exchange", async (Guid id, CompleteExchangeRequest? req, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CompleteExchangeReplacementCommand(id, req?.ReplacementVariantId ?? Guid.Empty, req?.ReplacementQuantity ?? 1), ct);
            return result.ToHttpResult(context);
        });

        return group;
    }
}
