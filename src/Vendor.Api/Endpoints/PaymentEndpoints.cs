using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class PaymentEndpoints
{
    public static RouteGroupBuilder MapPaymentEndpoints(this RouteGroupBuilder group)
    {
        var payments = group.MapGroup("/payments")
            .WithTags("Payments")
            .RequireAuthorization();

        payments.MapGet("/{id:guid}", async (Guid id, ISender mediator) =>
        {
            return Results.Ok(new PaymentDto(id, Guid.NewGuid(), "stripe", "Captured", new MoneyDto(100m, "USD"), "pi_123", DateTime.UtcNow));
        });

        payments.MapGet("/order/{orderId:guid}", async (Guid orderId, ISender mediator) =>
        {
            return Results.Ok(new PaymentDto(Guid.NewGuid(), orderId, "stripe", "Captured", new MoneyDto(100m, "USD"), "pi_123", DateTime.UtcNow));
        });

        var adminPayments = group.MapGroup("/admin/payments")
            .WithTags("Admin Payments")
            .RequireAuthorization();

        adminPayments.MapPost("/{id:guid}/capture", async (Guid id, CapturePaymentRequest req, ISender mediator) =>
        {
            return Results.Ok(new PaymentDto(id, Guid.NewGuid(), "stripe", "Captured", req.Amount ?? new MoneyDto(100m, "USD"), "pi_123", DateTime.UtcNow));
        });

        adminPayments.MapPost("/{id:guid}/refund", async (Guid id, RefundPaymentRequest req, ISender mediator) =>
        {
            return Results.Ok(new PaymentDto(id, Guid.NewGuid(), "stripe", "Refunded", req.Amount, "re_123", DateTime.UtcNow));
        });

        // Webhook endpoints
        var webhooks = group.MapGroup("/webhooks")
            .WithTags("Webhooks")
            .RequireRateLimiting("webhook");

        webhooks.MapPost("/stripe", async (HttpContext ctx, ISender mediator) =>
        {
            return Results.Ok(new { received = true });
        });

        webhooks.MapPost("/paypal", async (HttpContext ctx, ISender mediator) =>
        {
            return Results.Ok(new { received = true });
        });

        webhooks.MapPost("/paymob", async (HttpContext ctx, ISender mediator) =>
        {
            return Results.Ok(new { received = true });
        });

        webhooks.MapPost("/shipping", async (HttpContext ctx, ISender mediator) =>
        {
            return Results.Ok(new { received = true });
        });

        return group;
    }
}
