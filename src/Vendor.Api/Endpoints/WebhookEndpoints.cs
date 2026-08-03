using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Application.Modules.Payments;

namespace Vendor.Api.Endpoints;

public static class WebhookEndpoints
{
    public static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
    {
        var webhooks = group.MapGroup("/webhooks").WithTags("Webhooks");

        webhooks.MapPost("/stripe", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var sigHeader = request.Headers["Stripe-Signature"].ToString();

            if (string.IsNullOrEmpty(sigHeader) || string.IsNullOrEmpty(rawBody))
            {
                return Results.BadRequest(new { Error = "Invalid Stripe webhook payload or signature." });
            }

            var command = new ProcessPaymentWebhookCommand("Stripe", sigHeader, rawBody);
            var result = await mediator.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
        });

        webhooks.MapPost("/paymob", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var hmacHeader = request.Headers["Paymob-HMAC"].ToString();

            if (string.IsNullOrEmpty(hmacHeader) || string.IsNullOrEmpty(rawBody))
            {
                return Results.BadRequest(new { Error = "Invalid Paymob webhook payload or signature." });
            }

            var command = new ProcessPaymentWebhookCommand("PayMob", hmacHeader, rawBody);
            var result = await mediator.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
        });

        webhooks.MapPost("/paypal", async (HttpRequest request, ISender mediator, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var transmissionId = request.Headers["PAYPAL-TRANSMISSION-ID"].ToString();

            if (string.IsNullOrEmpty(transmissionId) || string.IsNullOrEmpty(rawBody))
            {
                return Results.BadRequest(new { Error = "Invalid Paypal webhook payload or signature." });
            }

            var command = new ProcessPaymentWebhookCommand("PayPal", transmissionId, rawBody);
            var result = await mediator.Send(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { Error = result.Error.Description });
        });

        return group;
    }
}
