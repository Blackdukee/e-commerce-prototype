using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Returns.Commands;

public class RejectReturnRequestCommandHandler(
    IReturnRequestRepository returnRequestRepository)
    : IRequestHandler<RejectReturnRequestCommand, Result<ReturnRequestDto>>
{
    public async Task<Result<ReturnRequestDto>> Handle(RejectReturnRequestCommand request, CancellationToken ct)
    {
        var returnReq = await returnRequestRepository.GetByIdAsync(new ReturnRequestId(request.ReturnRequestId), ct);
        if (returnReq == null)
        {
            return Error.NotFound("ReturnRequest", request.ReturnRequestId);
        }

        returnReq.Reject(request.Reason);
        await returnRequestRepository.UpdateAsync(returnReq, ct);

        return ReturnRequestDto.FromDomain(returnReq);
    }
}
