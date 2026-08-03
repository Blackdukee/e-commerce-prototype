using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Api.Extensions;
using Vendor.Application.Interfaces;
using Vendor.Application.Modules.Cart;
using Vendor.Application.Modules.Orders.Commands;
using Vendor.Application.Modules.Orders.Dtos;
using AppAddressDto = Vendor.Application.Modules.Orders.Dtos.AddressDto;

namespace Vendor.Api.Endpoints;

public static class CartEndpoints
{
    public static RouteGroupBuilder MapCartEndpoints(this RouteGroupBuilder group)
    {
        var cart = group.MapGroup("/cart")
            .WithTags("Cart");

        cart.MapGet("/", async (Guid? cartId, ICurrentUserService user, ISender mediator, CancellationToken ct) =>
        {
            if (cartId.HasValue)
            {
                var result = await mediator.Send(new GetCartByIdQuery(cartId.Value), ct);
                return result.ToHttpResult();
            }

            if (user.CustomerId.HasValue)
            {
                var result = await mediator.Send(new GetCartByCustomerIdQuery(user.CustomerId.Value), ct);
                return result.ToHttpResult();
            }

            // Return a default active guest cart for anonymous requests without cartId
            return Results.Ok(new DTOs.CartDto(Guid.NewGuid(), Array.Empty<DTOs.CartItemDto>(), null, new DTOs.MoneyDto(0m, "USD"), new DTOs.MoneyDto(0m, "USD")));
        });

        cart.MapPost("/items", async (Guid? cartId, AddCartItemRequest req, ISender mediator, CancellationToken ct) =>
        {
            var targetCartId = cartId ?? Guid.NewGuid();
            var command = new AddCartItemCommand(targetCartId, req.VariantId, req.Quantity, 0m, "USD");
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        cart.MapPut("/items/{variantId:guid}", async (Guid? cartId, Guid variantId, UpdateCartItemRequest req, ISender mediator, CancellationToken ct) =>
        {
            if (!cartId.HasValue) return Results.BadRequest("cartId is required");
            var command = new UpdateCartItemQuantityCommand(cartId.Value, variantId, req.Quantity);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        cart.MapDelete("/items/{variantId:guid}", async (Guid? cartId, Guid variantId, ISender mediator, CancellationToken ct) =>
        {
            if (!cartId.HasValue) return Results.BadRequest("cartId is required");
            var command = new RemoveCartItemCommand(cartId.Value, variantId);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        cart.MapPost("/discounts", async (Guid cartId, ApplyDiscountRequest req, ISender mediator, CancellationToken ct) =>
        {
            var command = new ApplyCartDiscountCodeCommand(cartId, req.Code);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        cart.MapDelete("/discounts/{code}", async (Guid cartId, string code, ISender mediator, CancellationToken ct) =>
        {
            var command = new RemoveCartDiscountCodeCommand(cartId);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        cart.MapPost("/merge", async (Guid customerCartId, MergeCartRequest req, ISender mediator, CancellationToken ct) =>
        {
            if (!Guid.TryParse(req.GuestSessionId, out var guestCartId))
            {
                return Results.BadRequest(new { error = "GuestSessionId must be a valid GUID cart ID." });
            }
            var command = new MergeGuestCartCommand(guestCartId, customerCartId);
            var result = await mediator.Send(command, ct);
            return result.ToHttpResult();
        });

        // Checkout orchestrator endpoint
        group.MapPost("/orders/checkout", async (CheckoutRequest req, HttpContext context, ISender mediator, CancellationToken ct) =>
        {
            var cartId = req.CartId ?? Guid.NewGuid();
            var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey) || !Guid.TryParse(idempotencyKey, out _))
            {
                idempotencyKey = Guid.NewGuid().ToString();
            }
            var shippingAddress = new AppAddressDto(req.ShippingAddress.Street, req.ShippingAddress.City, req.ShippingAddress.State, req.ShippingAddress.ZipCode, req.ShippingAddress.CountryCode);
            var command = new CheckoutOrderCommand(cartId, shippingAddress, idempotencyKey);
            var result = await mediator.Send(command, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/orders/{result.Value?.Id}", result.Value) : result.ToHttpResult();
        }).WithTags("Orders");

        return group;
    }
}
