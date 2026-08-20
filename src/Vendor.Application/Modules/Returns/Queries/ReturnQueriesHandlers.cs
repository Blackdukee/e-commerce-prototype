using MediatR;
using Vendor.Application.Common.Models;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Returns.Dtos;
using Vendor.Domain.Aggregates.ReturnRequest;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Returns.Queries;

public class GetReturnByIdQueryHandler(
    IReturnRequestRepository returnRequestRepository)
    : IRequestHandler<GetReturnByIdQuery, Result<ReturnRequestDto>>
{
    public async Task<Result<ReturnRequestDto>> Handle(GetReturnByIdQuery request, CancellationToken ct)
    {
        var returnReq = await returnRequestRepository.GetByIdAsync(new ReturnRequestId(request.ReturnRequestId), ct);
        if (returnReq == null)
        {
            return Error.NotFound("ReturnRequest", request.ReturnRequestId);
        }

        return ReturnRequestDto.FromDomain(returnReq);
    }
}

public class GetAdminReturnsQueryHandler(
    IReturnRequestRepository returnRequestRepository)
    : IRequestHandler<GetAdminReturnsQuery, Result<PagedResult<ReturnRequestDto>>>
{
    public async Task<Result<PagedResult<ReturnRequestDto>>> Handle(GetAdminReturnsQuery request, CancellationToken ct)
    {
        ReturnRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReturnRequestStatus>(request.Status, true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var (items, totalCount) = await returnRequestRepository.GetPagedAsync(
            status,
            request.PageIndex,
            request.PageSize,
            ct);

        var dtos = items.Select(ReturnRequestDto.FromDomain).ToList();
        return new PagedResult<ReturnRequestDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
