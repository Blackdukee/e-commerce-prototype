using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Returns.Commands;

public class SubmitReturnRequestCommandHandler(
    IReturnRequestRepository returnRequestRepository,
    IOrderRepository orderRepository)
    : IRequestHandler<SubmitReturnRequestCommand, Result<ReturnRequestDto>>
{
    public async Task<Result<ReturnRequestDto>> Handle(SubmitReturnRequestCommand request, CancellationToken ct)
    {
        var order = await orderRepository.GetByIdAsync(new OrderId(request.OrderId), ct);
        if (order == null)
        {
            return Error.NotFound("Order", request.OrderId);
        }

        if (order.Status != OrderStatus.Delivered)
        {
            return Error.Failure("Return.InvalidOrderStatus", "Returns can only be submitted for delivered orders.");
        }

        var domainItems = request.Items.Select(i => new ReturnItem(
            i.OrderLineId,
            new ProductVariantId(i.VariantId),
            i.Quantity,
            i.Reason)).ToList();

        var returnRequest = new ReturnRequest(
            ReturnRequestId.New(),
            order.Id,
            new CustomerId(request.CustomerId),
            request.Reason,
            domainItems);

        await returnRequestRepository.AddAsync(returnRequest, ct);

        return ReturnRequestDto.FromDomain(returnRequest);
    }
}
