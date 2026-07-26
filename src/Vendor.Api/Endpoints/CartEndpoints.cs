using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class CartEndpoints
{
    public static RouteGroupBuilder MapCartEndpoints(this RouteGroupBuilder group)
    {
        var cart = group.MapGroup("/cart")
            .WithTags("Cart");

        cart.MapGet("/", async (ISender mediator) =>
        {
            return Results.Ok(new CartDto(Guid.NewGuid(), Array.Empty<CartItemDto>(), null, new MoneyDto(0m, "USD"), new MoneyDto(0m, "USD")));
        });

        cart.MapPost("/items", async (AddCartItemRequest req, ISender mediator) =>
        {
            return Results.Ok(new { message = "Item added to cart", variantId = req.VariantId });
        });

        cart.MapPut("/items/{variantId:guid}", async (Guid variantId, UpdateCartItemRequest req, ISender mediator) =>
        {
            return Results.Ok(new { message = "Cart item updated", quantity = req.Quantity });
        });

        cart.MapDelete("/items/{variantId:guid}", async (Guid variantId, ISender mediator) =>
        {
            return Results.Ok(new { message = "Cart item removed" });
        });

        cart.MapPost("/discounts", async (ApplyDiscountRequest req, ISender mediator) =>
        {
            return Results.Ok(new { message = "Discount applied", code = req.Code });
        });

        cart.MapDelete("/discounts/{code}", async (string code, ISender mediator) =>
        {
            return Results.Ok(new { message = "Discount removed" });
        });

        cart.MapPost("/merge", async (MergeCartRequest req, ISender mediator) =>
        {
            return Results.Ok(new { message = "Guest cart merged successfully" });
        });

        // Checkout orchestrator endpoint
        group.MapPost("/orders/checkout", async (CheckoutRequest req, ISender mediator) =>
        {
            return Results.Created($"/api/v1/orders/{Guid.NewGuid()}", new CheckoutResponseDto(
                Guid.NewGuid(),
                "ORD-9999",
                new MoneyDto(100m, "USD"),
                new PaymentInitDto(req.PaymentProvider, "client_secret_test", null, null)
            ));
        }).WithTags("Orders");

        return group;
    }
}
