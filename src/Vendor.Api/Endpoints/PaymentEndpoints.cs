using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Application.Commands.Payments.ProcessPayment;
using Vendor.Application.Commands.Payments.ProcessWebhook;
using Vendor.Application.Queries.Payments.GetPaymentLedger;

namespace Vendor.Api.Endpoints;

public record ProcessPaymentApiRequest(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string ProviderName
);

public record WebhookApiPayload(
    string? EventId,
    string? EventType,
    Guid? PaymentId,
    decimal? Amount,
    string? Currency,
    string? GatewayReferenceId
);

public static class PaymentEndpoints
{
    public static RouteGroupBuilder MapPaymentEndpoints(this RouteGroupBuilder group)
    {
        var payments = group.MapGroup("/payments")
            .WithTags("Payments");

        payments.MapPost("/process", async (HttpContext context, ProcessPaymentApiRequest req, ISender mediator) =>
        {
            if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues) ||
                string.IsNullOrWhiteSpace(keyValues.ToString()) ||
                !Guid.TryParse(keyValues.ToString(), out var idempotencyKeyUuid))
            {
                return Results.BadRequest(new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    title = "Validation Error",
                    status = 400,
                    errors = new Dictionary<string, string[]>
                    {
                        ["Idempotency-Key"] = ["The Idempotency-Key header is required and must be a valid UUID v4."]
                    }
                });
            }

            var command = new ProcessPaymentCommand(
                req.OrderId,
                req.Amount,
                req.Currency,
                req.PaymentMethod,
                req.ProviderName,
                idempotencyKeyUuid.ToString()
            );

            var result = await mediator.Send(command);

            if (result.IsFailure)
            {
                if (result.Error.Code == "IDEMPOTENCY_PAYLOAD_MISMATCH")
                {
                    return Results.UnprocessableEntity(new
                    {
                        type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                        title = "Unprocessable Entity",
                        status = 422,
                        detail = result.Error.Description
                    });
                }

                return Results.BadRequest(new
                {
                    title = "Payment Process Error",
                    status = 400,
                    detail = result.Error.Description
                });
            }

            return Results.Created($"/api/v1/payments/{result.Value.PaymentId}", result.Value);
        });

        payments.MapGet("/{paymentId:guid}/ledger", async (Guid paymentId, ISender mediator) =>
        {
            var query = new GetPaymentLedgerQuery(paymentId);
            var result = await mediator.Send(query);

            if (result.IsFailure)
            {
                return Results.NotFound(new
                {
                    title = "Payment Ledger Not Found",
                    status = 404,
                    detail = result.Error.Description
                });
            }

            return Results.Ok(result.Value);
        });

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

        // Webhook ingestion endpoints
        var webhooks = group.MapGroup("/webhooks")
            .WithTags("Webhooks");

        webhooks.MapPost("/{providerName}", async (string providerName, HttpContext ctx, WebhookApiPayload payload, ISender mediator) =>
        {
            var sigHeader = ctx.Request.Headers["Stripe-Signature"].ToString();
            if (string.IsNullOrEmpty(sigHeader))
            {
                sigHeader = ctx.Request.Headers["X-Paymob-Signature"].ToString();
            }
            if (string.IsNullOrEmpty(sigHeader))
            {
                sigHeader = ctx.Request.Headers["Paypal-Transmission-Sig"].ToString();
            }

            var eventId = string.IsNullOrWhiteSpace(payload.EventId) ? $"evt_{Guid.NewGuid():N}" : payload.EventId;
            var eventType = string.IsNullOrWhiteSpace(payload.EventType) ? "payment_intent.succeeded" : payload.EventType;
            var paymentId = payload.PaymentId ?? Guid.NewGuid();
            var amount = payload.Amount ?? 100m;
            var currency = string.IsNullOrWhiteSpace(payload.Currency) ? "USD" : payload.Currency;

            var command = new ProcessWebhookCommand(
                providerName,
                sigHeader,
                RawPayload: System.Text.Json.JsonSerializer.Serialize(payload),
                eventId,
                eventType,
                paymentId,
                amount,
                currency,
                payload.GatewayReferenceId
            );

            var result = await mediator.Send(command);

            if (result.IsFailure)
            {
                if (result.Error.Code == "Auth.Unauthorized")
                {
                    return Results.Unauthorized();
                }

                return Results.BadRequest(new { error = result.Error.Description });
            }

            return Results.Ok(result.Value);
        });

        return group;
    }
}
