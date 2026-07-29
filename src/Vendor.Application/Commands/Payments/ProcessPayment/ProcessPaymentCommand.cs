using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.Payment.Enums;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Commands.Payments.ProcessPayment;

public record ProcessPaymentResponseDto(
    Guid PaymentId,
    Guid OrderId,
    string Status,
    decimal Amount,
    string Currency,
    string? GatewayReferenceId,
    string IdempotencyKey,
    DateTime CreatedAtUtc
);

public record ProcessPaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string ProviderName,
    string IdempotencyKey
) : ICommand<Result<ProcessPaymentResponseDto>>, IIdempotentRequest<Result<ProcessPaymentResponseDto>>;

public class ProcessPaymentCommandHandler(
    IPaymentLedgerRepository ledgerRepository,
    IPaymentRepository paymentRepository)
    : IRequestHandler<ProcessPaymentCommand, Result<ProcessPaymentResponseDto>>
{
    public async Task<Result<ProcessPaymentResponseDto>> Handle(ProcessPaymentCommand request, CancellationToken ct)
    {
        if (request.Amount <= 0m)
        {
            return Error.Failure("INVALID_AMOUNT", "Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return Error.Failure("INVALID_CURRENCY", "Currency code is required.");
        }

        var paymentId = PaymentId.New();
        var orderId = new OrderId(request.OrderId);
        var correlationId = Guid.NewGuid().ToString("N");

        // 1. Write Intent to the immutable ledger (Sequence 1)
        var intentEntry = new PaymentLedgerEntry(
            paymentId,
            sequenceNumber: 1,
            eventType: PaymentLedgerEventType.Intent,
            amount: new Money(request.Amount, request.Currency),
            gatewayReferenceId: null,
            failureReason: null,
            correlationId: correlationId
        );
        await ledgerRepository.AddAsync(intentEntry, ct);

        // 2. Create base Payment aggregate
        var payment = new Payment(paymentId, orderId, new Money(request.Amount, request.Currency), request.IdempotencyKey);
        await paymentRepository.AddAsync(payment, ct);

        // 3. Simulate payment authorization
        var gatewayTxnId = $"gtw_{Guid.NewGuid():N}";
        payment.Capture(gatewayTxnId);

        // 4. Append Authorized entry to the ledger (Sequence 2)
        var authorizedEntry = new PaymentLedgerEntry(
            paymentId,
            sequenceNumber: 2,
            eventType: PaymentLedgerEventType.Authorized,
            amount: new Money(request.Amount, request.Currency),
            gatewayReferenceId: gatewayTxnId,
            failureReason: null,
            correlationId: correlationId
        );
        await ledgerRepository.AddAsync(authorizedEntry, ct);

        var dto = new ProcessPaymentResponseDto(
            paymentId.Value,
            request.OrderId,
            PaymentStatus.Authorized.ToString(),
            request.Amount,
            request.Currency,
            gatewayTxnId,
            request.IdempotencyKey,
            DateTime.UtcNow
        );

        return Result<ProcessPaymentResponseDto>.Success(dto);
    }
}
