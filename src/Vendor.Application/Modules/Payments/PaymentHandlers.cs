using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Payments;

public record PaymentDto(Guid Id, Guid OrderId, string Status, decimal Amount, string Currency, string IdempotencyKey, string? GatewayTransactionId, DateTime? CapturedAtUtc)
{
    public static PaymentDto FromDomain(Payment payment) => new(
        payment.Id.Value,
        payment.OrderId.Value,
        payment.Status.ToString(),
        payment.Amount.Amount,
        payment.Amount.Currency,
        payment.IdempotencyKey,
        payment.GatewayTransactionId,
        payment.CapturedAtUtc);
}

public record AuthorizePaymentCommand(Guid OrderId, decimal Amount, string Currency, string IdempotencyKey) : ICommand<Result<PaymentDto>>, IIdempotentRequest<Result<PaymentDto>>;
public record CapturePaymentCommand(Guid PaymentId, string GatewayTransactionId) : ICommand<Result<PaymentDto>>, IIdempotentRequest<Result<PaymentDto>>
{
    public string IdempotencyKey => $"CAP-{PaymentId}-{GatewayTransactionId}";
}
public record FailPaymentCommand(Guid PaymentId, string Reason) : ICommand<Result<PaymentDto>>, IIdempotentRequest<Result<PaymentDto>>
{
    public string IdempotencyKey => $"FAIL-PAY-{PaymentId}";
}
public record RefundPaymentCommand(Guid PaymentId, decimal RefundAmount, string Currency, string IdempotencyKey) : ICommand<Result<PaymentDto>>, IIdempotentRequest<Result<PaymentDto>>;

public record GetPaymentByIdQuery(Guid PaymentId) : IQuery<Result<PaymentDto>>;
public record GetPaymentByOrderIdQuery(Guid OrderId) : IQuery<Result<PaymentDto>>;
public record GetPaymentByIdempotencyKeyQuery(string IdempotencyKey) : IQuery<Result<PaymentDto>>;

public class GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository) : IRequestHandler<GetPaymentByIdQuery, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(GetPaymentByIdQuery request, CancellationToken ct)
    {
        var payment = await paymentRepository.GetByIdAsync(new PaymentId(request.PaymentId), ct);
        if (payment == null) return Error.NotFound("Payment", request.PaymentId);
        return PaymentDto.FromDomain(payment);
    }
}
