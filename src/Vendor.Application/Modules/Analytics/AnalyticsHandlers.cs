using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.AnalyticsEvent;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Analytics;

public record AnalyticsEventDto(Guid Id, Guid? CustomerId, string EventType, string Payload, bool ConsentGrantedAtCapture, DateTime OccurredAtUtc)
{
    public static AnalyticsEventDto FromDomain(AnalyticsEvent evt) => new(
        evt.Id.Value,
        evt.CustomerId?.Value,
        evt.EventType,
        evt.Payload,
        evt.ConsentGrantedAtCapture,
        evt.OccurredAtUtc);
}

public record CaptureAnalyticsEventCommand(Guid? CustomerId, string EventType, string Payload, bool ConsentGranted) : ICommand<Result<AnalyticsEventDto>>;
public record ForwardAnalyticsEventsCommand(List<Guid> EventIds) : ICommand<Result<int>>, IIdempotentRequest<Result<int>>
{
    public string IdempotencyKey => $"FWD-ANALYTICS-{EventIds.GetHashCode()}";
}

public record GetCustomerAnalyticsHistoryQuery(Guid CustomerId, int PageIndex = 0, int PageSize = 50) : IQuery<Result<IReadOnlyList<AnalyticsEventDto>>>;

public class CaptureAnalyticsEventCommandHandler(IAnalyticsEventRepository analyticsEventRepository) : IRequestHandler<CaptureAnalyticsEventCommand, Result<AnalyticsEventDto>>
{
    public async Task<Result<AnalyticsEventDto>> Handle(CaptureAnalyticsEventCommand request, CancellationToken ct)
    {
        CustomerId? custId = request.CustomerId.HasValue ? new CustomerId(request.CustomerId.Value) : null;
        var evt = AnalyticsEvent.Capture(custId, request.EventType, request.Payload, request.ConsentGranted);

        await analyticsEventRepository.AddAsync(evt, ct);
        return AnalyticsEventDto.FromDomain(evt);
    }
}
