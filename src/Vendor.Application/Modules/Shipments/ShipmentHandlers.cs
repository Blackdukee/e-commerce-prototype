using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Modules.Orders.Dtos;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Shipment;
using Vendor.Domain.Interfaces.Repositories;

namespace Vendor.Application.Modules.Shipments;

public record ShipmentDto(Guid Id, Guid OrderId, string Status, string? TrackingNumber, string CarrierCode, AddressDto ShippingAddress, DateTime? EstimatedDeliveryUtc)
{
    public static ShipmentDto FromDomain(Shipment shipment) => new(
        shipment.Id.Value,
        shipment.OrderId.Value,
        shipment.Status.ToString(),
        shipment.TrackingNumber,
        shipment.CarrierCode,
        AddressDto.FromDomain(shipment.ShippingAddress),
        shipment.EstimatedDeliveryUtc);
}

public record CreateShipmentLabelCommand(Guid OrderId, string CarrierCode, string TrackingNumber, DateTime? EstimatedDelivery = null) : ICommand<Result<ShipmentDto>>, IIdempotentRequest<Result<ShipmentDto>>
{
    public string IdempotencyKey => $"LABEL-{OrderId}-{TrackingNumber}";
}
public record MarkShipmentInTransitCommand(Guid ShipmentId) : ICommand<Result<ShipmentDto>>, IIdempotentRequest<Result<ShipmentDto>>
{
    public string IdempotencyKey => $"INTRANSIT-{ShipmentId}";
}
public record MarkShipmentOutForDeliveryCommand(Guid ShipmentId) : ICommand<Result<ShipmentDto>>, IIdempotentRequest<Result<ShipmentDto>>
{
    public string IdempotencyKey => $"OUTDELIV-{ShipmentId}";
}
public record MarkShipmentDeliveredCommand(Guid ShipmentId) : ICommand<Result<ShipmentDto>>, IIdempotentRequest<Result<ShipmentDto>>
{
    public string IdempotencyKey => $"DELIVERED-{ShipmentId}";
}
public record MarkShipmentFailedCommand(Guid ShipmentId, string Reason) : ICommand<Result<ShipmentDto>>, IIdempotentRequest<Result<ShipmentDto>>
{
    public string IdempotencyKey => $"FAILSHIP-{ShipmentId}";
}

public record GetShipmentByIdQuery(Guid ShipmentId) : IQuery<Result<ShipmentDto>>;
public record GetShipmentByOrderIdQuery(Guid OrderId) : IQuery<Result<ShipmentDto>>;
public record TrackShipmentQuery(string TrackingNumber, string CarrierCode) : IQuery<Result<ShipmentDto>>;

public class GetShipmentByIdQueryHandler(IShipmentRepository shipmentRepository) : IRequestHandler<GetShipmentByIdQuery, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(GetShipmentByIdQuery request, CancellationToken ct)
    {
        var shipment = await shipmentRepository.GetByIdAsync(new ShipmentId(request.ShipmentId), ct);
        if (shipment == null) return Error.NotFound("Shipment", request.ShipmentId);
        return ShipmentDto.FromDomain(shipment);
    }
}
