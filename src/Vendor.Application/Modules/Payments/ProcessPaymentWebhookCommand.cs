using MediatR;
using Microsoft.Extensions.Logging;
using Vendor.Application.Common.Interfaces;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Entities;
using Vendor.Domain.Events;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Payments;

public record ProcessPaymentWebhookCommand(
    string Provider,
    string SignatureHeader,
    string RawBody
) : ICommand<Result<bool>>;

public class ProcessPaymentWebhookCommandHandler(
    IWebhookParserFactory parserFactory,
    IWebhookEventRepository webhookEventRepository,
    IOutboxService outboxService,
    ILogger<ProcessPaymentWebhookCommandHandler> logger)
    : IRequestHandler<ProcessPaymentWebhookCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ProcessPaymentWebhookCommand request, CancellationToken ct)
    {
        // 1. Verify cryptographic signature and parse event payload
        var parseResult = parserFactory.ParseAndVerify(request.Provider, request.RawBody, request.SignatureHeader);

        if (!parseResult.IsValid)
        {
            logger.LogWarning("Security Warning: Invalid {Provider} webhook signature attempt.", request.Provider);
            return Result<bool>.Failure(Error.Failure("Webhook.InvalidSignature", "Invalid signature"));
        }

        // 2. Check for event deduplication (replay protection)
        var exists = await webhookEventRepository.ExistsAsync(request.Provider, parseResult.EventId, ct);
        if (exists)
        {
            logger.LogInformation("Webhook event {EventId} for provider {Provider} already processed.", parseResult.EventId, request.Provider);
            return Result<bool>.Success(true);
        }

        // 3. Create WebhookEvent and persist to database
        var webhookEvent = new WebhookEvent(
            Guid.NewGuid(),
            request.Provider,
            parseResult.EventId,
            parseResult.EventType,
            request.RawBody
        );

        await webhookEventRepository.AddAsync(webhookEvent, ct);

        // 4. Publish domain event via Outbox
        var orderId = parseResult.OrderId ?? Guid.NewGuid();
        if (parseResult.IsPaymentSuccess)
        {
            var domainEvent = new OrderPaymentSucceededEvent(
                new OrderId(orderId),
                request.Provider,
                GatewayEventId: parseResult.EventId,
                new Money(parseResult.Amount, parseResult.Currency),
                DateTime.UtcNow
            );
            await outboxService.SaveAndPublishAsync(domainEvent, ct);
        }
        else
        {
            var domainEvent = new OrderPaymentFailedEvent(
                new OrderId(orderId),
                request.Provider,
                GatewayEventId: parseResult.EventId,
                parseResult.FailureReason ?? "Payment failed",
                DateTime.UtcNow
            );
            await outboxService.SaveAndPublishAsync(domainEvent, ct);
        }


        return Result<bool>.Success(true);
    }
}
