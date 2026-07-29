using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Payment.Enums;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Commands.Payments.ProcessWebhook;

public record ProcessWebhookResponseDto(
    string EventId,
    string Status
);

public record ProcessWebhookCommand(
    string ProviderName,
    string SignatureHeader,
    string RawPayload,
    string EventId,
    string EventType,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string? GatewayReferenceId
) : ICommand<Result<ProcessWebhookResponseDto>>;

public class ProcessWebhookCommandHandler(
    IPaymentGateway paymentGateway,
    IWebhookEventRepository webhookEventRepository,
    IPaymentLedgerRepository ledgerRepository,
    IPaymentRepository paymentRepository)
    : IRequestHandler<ProcessWebhookCommand, Result<ProcessWebhookResponseDto>>
{
    public async Task<Result<ProcessWebhookResponseDto>> Handle(ProcessWebhookCommand request, CancellationToken ct)
    {
        // 1. Verify cryptographic signature
        if (string.IsNullOrWhiteSpace(request.SignatureHeader))
        {
            return Error.Unauthorized("Invalid cryptographic webhook signature.");
        }

        var isSignatureValid = await paymentGateway.VerifyWebhookSignatureAsync(
            request.RawPayload,
            request.SignatureHeader,
            secret: "secret_ref",
            ct: ct);

        if (!isSignatureValid)
        {
            return Error.Unauthorized("Invalid cryptographic webhook signature.");
        }

        // 2. Check for event deduplication
        var existingEvent = await webhookEventRepository.GetByGatewayAndEventIdAsync(request.ProviderName, request.EventId, ct);
        if (existingEvent is not null)
        {
            return Result<ProcessWebhookResponseDto>.Success(new ProcessWebhookResponseDto(request.EventId, "SkippedDuplicate"));
        }

        var payloadHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.RawPayload)));
        var webhookEntry = new WebhookEventEntry(request.ProviderName, request.EventId, request.EventType, payloadHash);
        await webhookEventRepository.AddAsync(webhookEntry, ct);

        // 3. Retry loop to find payment if intent commit was transiently delayed (3 retries with exponential backoff)
        var paymentId = new PaymentId(request.PaymentId);
        Payment? payment = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            payment = await paymentRepository.GetByIdAsync(paymentId, ct);
            if (payment is not null)
            {
                break;
            }
            if (attempt < 3)
            {
                await Task.Delay((int)(Math.Pow(1.5, attempt) * 1000), ct);
            }
        }

        // 4. Determine state transition and append to ledger
        var nextSeq = await ledgerRepository.GetNextSequenceNumberAsync(paymentId, ct);
        var eventType = MapEventType(request.EventType);

        var ledgerEntry = new PaymentLedgerEntry(
            paymentId,
            nextSeq,
            eventType,
            new Money(request.Amount, request.Currency),
            request.GatewayReferenceId,
            failureReason: null,
            correlationId: $"wh_{request.EventId}"
        );
        await ledgerRepository.AddAsync(ledgerEntry, ct);

        webhookEntry.MarkProcessed();

        return Result<ProcessWebhookResponseDto>.Success(new ProcessWebhookResponseDto(request.EventId, "Processed"));
    }

    private static PaymentLedgerEventType MapEventType(string eventTypeStr)
    {
        if (eventTypeStr.Contains("captured", StringComparison.OrdinalIgnoreCase) ||
            eventTypeStr.Contains("succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentLedgerEventType.Captured;
        }

        if (eventTypeStr.Contains("refund", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentLedgerEventType.Refunded;
        }

        if (eventTypeStr.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentLedgerEventType.Failed;
        }

        return PaymentLedgerEventType.Authorized;
    }
}
