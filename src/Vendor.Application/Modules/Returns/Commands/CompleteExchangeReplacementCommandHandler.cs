using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Repositories;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Returns.Commands;

public class CompleteExchangeReplacementCommandHandler(
    IReturnRequestRepository returnRequestRepository,
    IProductRepository productRepository,
    IOrderRepository orderRepository)
    : IRequestHandler<CompleteExchangeReplacementCommand, Result<ReturnRequestDto>>
{
    public async Task<Result<ReturnRequestDto>> Handle(CompleteExchangeReplacementCommand request, CancellationToken ct)
    {
        var returnReq = await returnRequestRepository.GetByIdAsync(new ReturnRequestId(request.ReturnRequestId), ct);
        if (returnReq == null)
        {
            return Error.NotFound("ReturnRequest", request.ReturnRequestId);
        }

        if (returnReq.RequestedResolution != ResolutionType.Exchange)
        {
            return Error.Failure("Return.InvalidResolution", "Return request resolution is not Exchange.");
        }

        // Restock original items
        foreach (var item in returnReq.Items)
        {
            var product = await productRepository.GetByIdAsync(new ProductId(item.ProductVariantId.Value), ct);
            if (product != null)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId);
                variant?.AddStock(item.Quantity);
                await productRepository.UpdateAsync(product, ct);
            }
        }

        // Create replacement order
        var originalOrder = await orderRepository.GetByIdAsync(returnReq.OrderId, ct);
        OrderId? replacementOrderId = null;

        if (originalOrder != null)
        {
            replacementOrderId = OrderId.New();
            var line = new OrderLine(
                replacementOrderId.Value,
                new ProductVariantId(request.ReplacementVariantId),
                "Replacement Item",
                "REP-SKU",
                request.ReplacementQuantity,
                Money.Zero("USD"));

            var replacementOrder = new Order(
                replacementOrderId.Value,
                originalOrder.CustomerId,
                $"EXC-{originalOrder.OrderNumber}",
                originalOrder.ShippingAddress,
                [line],
                Money.Zero("USD"),
                Money.Zero("USD"),
                Money.Zero("USD"));

            await orderRepository.AddAsync(replacementOrder, ct);
        }

        returnReq.CompleteExchange(replacementOrderId);
        await returnRequestRepository.UpdateAsync(returnReq, ct);

        return ReturnRequestDto.FromDomain(returnReq);
    }
}
