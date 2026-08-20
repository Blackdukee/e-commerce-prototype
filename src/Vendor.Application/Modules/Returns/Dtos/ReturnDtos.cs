using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Models;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.ReturnRequest;

namespace Vendor.Application.Modules.Returns.Dtos;

public record ReturnItemInputDto(Guid OrderLineId, Guid VariantId, int Quantity, string Reason);

public record ReturnItemDto(Guid OrderLineId, Guid VariantId, int Quantity, string Reason);

public record ReturnRequestDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    string Status,
    string? Reason,
    string RequestedResolution,
    DateTime CreatedAtUtc,
    IReadOnlyList<ReturnItemDto> Items)
{
    public static ReturnRequestDto FromDomain(ReturnRequest request) => new(
        request.Id.Value,
        request.OrderId.Value,
        request.CustomerId.Value,
        request.Status.ToString(),
        request.Reason,
        request.RequestedResolution.ToString(),
        request.CreatedAtUtc,
        request.Items.Select(i => new ReturnItemDto(i.OrderLineId, i.ProductVariantId.Value, i.Quantity, i.Reason)).ToList());
}

public record SubmitReturnRequestCommand(
    Guid OrderId,
    Guid CustomerId,
    string Reason,
    List<ReturnItemInputDto> Items,
    ResolutionType RequestedResolution = ResolutionType.Refund) : ICommand<Result<ReturnRequestDto>>;

public record ApproveReturnRequestCommand(
    Guid ReturnRequestId,
    ResolutionType Resolution) : ICommand<Result<ReturnRequestDto>>;

public record RejectReturnRequestCommand(
    Guid ReturnRequestId,
    string Reason) : ICommand<Result<ReturnRequestDto>>;

public record MarkReturnItemsReceivedCommand(
    Guid ReturnRequestId) : ICommand<Result<ReturnRequestDto>>;

public record CompleteReturnRefundCommand(
    Guid ReturnRequestId) : ICommand<Result<ReturnRequestDto>>;

public record CompleteExchangeReplacementCommand(
    Guid ReturnRequestId,
    Guid ReplacementVariantId,
    int ReplacementQuantity) : ICommand<Result<ReturnRequestDto>>;

public record GetReturnByIdQuery(Guid ReturnRequestId) : IQuery<Result<ReturnRequestDto>>;

public record GetAdminReturnsQuery(string? Status = null, int PageIndex = 0, int PageSize = 20) : IQuery<Result<PagedResult<ReturnRequestDto>>>;

