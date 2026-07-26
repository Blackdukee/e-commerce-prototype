using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Domain.Aggregates.Cart;
using Vendor.Domain.Aggregates.Customer;
using Vendor.Domain.Aggregates.Order;
using Vendor.Domain.Aggregates.Product;
using Vendor.Domain.ValueObjects;

namespace Vendor.Application.Modules.Orders.Dtos;

public record AddressDto(string Street, string City, string State, string ZipCode, string CountryCode)
{
    public Address ToDomain() => new(Street, City, State, ZipCode, CountryCode);
    public static AddressDto FromDomain(Address address) =>
        new(address.Street, address.City, address.State, address.ZipCode, address.CountryCode);
}

public record OrderLineDto(Guid VariantId, string ProductName, string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string Status,
    AddressDto ShippingAddress,
    decimal Subtotal,
    decimal Tax,
    decimal ShippingCost,
    decimal Discount,
    decimal Total,
    DateTime PlacedAtUtc,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderDto FromDomain(Order order) => new(
        order.Id.Value,
        order.OrderNumber,
        order.CustomerId.Value,
        order.Status.ToString(),
        AddressDto.FromDomain(order.ShippingAddress),
        order.Subtotal.Amount,
        order.Tax.Amount,
        order.ShippingCost.Amount,
        order.Discount.Amount,
        order.Total.Amount,
        order.PlacedAtUtc,
        order.Lines.Select(l => new OrderLineDto(
            l.ProductVariantId.Value,
            l.ProductName,
            l.Sku,
            l.Quantity,
            l.UnitPrice.Amount,
            l.LineTotal.Amount)).ToList());
}

public record CheckoutOrderCommand(
    Guid CartId,
    AddressDto ShippingAddress,
    string IdempotencyKey) : ICommand<Result<OrderDto>>, IIdempotentRequest<Result<OrderDto>>;
