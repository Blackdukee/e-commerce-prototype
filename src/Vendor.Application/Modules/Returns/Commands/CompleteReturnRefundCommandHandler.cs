using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.Payment;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Adapters;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Returns.Commands;

public class CompleteReturnRefundCommandHandler(
    IReturnRequestRepository returnRequestRepository,
    IProductRepository productRepository,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway)
    : IRequestHandler<CompleteReturnRefundCommand, Result<ReturnRequestDto>>
{
    public async Task<Result<ReturnRequestDto>> Handle(CompleteReturnRefundCommand request, CancellationToken ct)
    {
        var returnReq = await returnRequestRepository.GetByIdAsync(new ReturnRequestId(request.ReturnRequestId), ct);
        if (returnReq == null)
        {
            return Error.NotFound("ReturnRequest", request.ReturnRequestId);
        }

        if (returnReq.RequestedResolution != ResolutionType.Refund)
        {
            return Error.Failure("Return.InvalidResolution", "Return request resolution is not Refund.");
        }

        // Restock returned items
        foreach (var item in returnReq.Items)
        {
            var product = await productRepository.GetByIdAsync(new Domain.Aggregates.Product.ProductId(item.ProductVariantId.Value), ct);
            if (product != null)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId);
                variant?.AddStock(item.Quantity);
                await productRepository.UpdateAsync(product, ct);
            }
        }

        // Refund payment
        var payment = await paymentRepository.GetByOrderIdAsync(returnReq.OrderId, ct);
        if (payment != null && payment.GatewayTransactionId != null)
        {
            var refundResult = await paymentGateway.RefundAsync(
                payment.GatewayTransactionId,
                payment.Amount,
                $"REFUND-{returnReq.Id.Value}",
                ct);

            if (refundResult.Success)
            {
                payment.Refund(payment.Amount);
                await paymentRepository.UpdateAsync(payment, ct);
            }
        }

        returnReq.CompleteReturn();
        await returnRequestRepository.UpdateAsync(returnReq, ct);

        return ReturnRequestDto.FromDomain(returnReq);
    }
}
