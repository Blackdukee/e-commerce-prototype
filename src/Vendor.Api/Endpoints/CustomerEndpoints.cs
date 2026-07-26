using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class CustomerEndpoints
{
    public static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder group)
    {
        var customer = group.MapGroup("/customer")
            .WithTags("Customer")
            .RequireAuthorization();

        customer.MapGet("/profile", async (ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new CustomerDto(Guid.NewGuid(), "customer@example.com", "John", "Doe", "Registered", true));
        });

        customer.MapPut("/addresses", async (AddressDto req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new[] { req });
        });

        customer.MapPut("/consent", async (UpdateConsentRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.NoContent();
        });

        customer.MapPost("/convert-guest", async (ConvertGuestRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new { message = "Guest converted to registered customer" });
        });

        return group;
    }
}
