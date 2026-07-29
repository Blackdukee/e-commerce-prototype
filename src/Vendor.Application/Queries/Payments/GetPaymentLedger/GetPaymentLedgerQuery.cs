using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Queries.Payments.GetPaymentLedger;

public record PaymentLedgerItemDto(
    int SequenceNumber,
    string EventType,
    decimal Amount,
    string Currency,
    string? GatewayReferenceId,
    DateTime CreatedAtUtc
);

public record PaymentLedgerTimelineDto(
    Guid PaymentId,
    string CurrentStatus,
    IReadOnlyList<PaymentLedgerItemDto> Timeline
);

public record GetPaymentLedgerQuery(Guid PaymentId) : IQuery<Result<PaymentLedgerTimelineDto>>;

public class GetPaymentLedgerQueryHandler(
    IPaymentLedgerRepository ledgerRepository,
    IPaymentRepository paymentRepository)
    : IRequestHandler<GetPaymentLedgerQuery, Result<PaymentLedgerTimelineDto>>
{
    public async Task<Result<PaymentLedgerTimelineDto>> Handle(GetPaymentLedgerQuery request, CancellationToken ct)
    {
        var paymentId = new PaymentId(request.PaymentId);
        var entries = await ledgerRepository.GetByPaymentIdAsync(paymentId, ct);

        if (entries.Count == 0)
        {
            return Error.NotFound("Payment", request.PaymentId);
        }

        var payment = await paymentRepository.GetByIdAsync(paymentId, ct);
        var currentStatus = payment?.Status.ToString() ?? entries.Last().EventType.ToString();

        var timelineItems = entries
            .Select(e => new PaymentLedgerItemDto(
                e.SequenceNumber,
                e.EventType.ToString(),
                e.Amount.Amount,
                e.Amount.Currency,
                e.GatewayReferenceId,
                e.CreatedAtUtc
            ))
            .ToList();

        var dto = new PaymentLedgerTimelineDto(
            request.PaymentId,
            currentStatus,
            timelineItems
        );

        return Result<PaymentLedgerTimelineDto>.Success(dto);
    }
}
