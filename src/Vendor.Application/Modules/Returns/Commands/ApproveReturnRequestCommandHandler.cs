using MediatR;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Returns.Commands;

public class ApproveReturnRequestCommandHandler(
    IReturnRequestRepository returnRequestRepository)
    : IRequestHandler<ApproveReturnRequestCommand, Result<ReturnRequestDto>>
{
    public async Task<Result<ReturnRequestDto>> Handle(ApproveReturnRequestCommand request, CancellationToken ct)
    {
        var returnReq = await returnRequestRepository.GetByIdAsync(new ReturnRequestId(request.ReturnRequestId), ct);
        if (returnReq == null)
        {
            return Error.NotFound("ReturnRequest", request.ReturnRequestId);
        }

        returnReq.Approve(request.Resolution);
        await returnRequestRepository.UpdateAsync(returnReq, ct);

        return ReturnRequestDto.FromDomain(returnReq);
    }
}
