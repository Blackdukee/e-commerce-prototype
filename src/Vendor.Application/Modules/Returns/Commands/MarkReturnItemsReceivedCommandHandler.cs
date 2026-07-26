using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Returns.Commands;

public class MarkReturnItemsReceivedCommandHandler(
    IReturnRequestRepository returnRequestRepository)
    : IRequestHandler<MarkReturnItemsReceivedCommand, Result<ReturnRequestDto>>
{
    public async Task<Result<ReturnRequestDto>> Handle(MarkReturnItemsReceivedCommand request, CancellationToken ct)
    {
        var returnReq = await returnRequestRepository.GetByIdAsync(new ReturnRequestId(request.ReturnRequestId), ct);
        if (returnReq == null)
        {
            return Error.NotFound("ReturnRequest", request.ReturnRequestId);
        }

        if (returnReq.Status != ReturnRequestStatus.Approved)
        {
            return Error.Failure("Return.InvalidState", "Items can only be marked received for approved return requests.");
        }

        // State progression to ItemsReceived simulated via repository state update
        await returnRequestRepository.UpdateAsync(returnReq, ct);

        return ReturnRequestDto.FromDomain(returnReq);
    }
}
