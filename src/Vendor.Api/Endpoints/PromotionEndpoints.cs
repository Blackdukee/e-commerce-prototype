using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;

namespace Vendor.Api.Endpoints;

public static class PromotionEndpoints
{
    public static RouteGroupBuilder MapPromotionEndpoints(this RouteGroupBuilder group)
    {
        var promotions = group.MapGroup("/promotions")
            .WithTags("Promotions");

        promotions.MapPost("/validate", async (ValidatePromotionRequest req, ISender mediator) =>
        {
            return Results.Ok(new ValidatePromotionResponse(true, new MoneyDto(10m, "USD"), null));
        });

        var adminPromotions = group.MapGroup("/admin/promotions")
            .WithTags("Admin Promotions")
            .RequireAuthorization();

        adminPromotions.MapPost("/", async (CreatePromotionRequest req, ISender mediator) =>
        {
            return Results.Created($"/api/v1/admin/promotions/{Guid.NewGuid()}", new PromotionDto(
                Guid.NewGuid(), req.Code, req.DiscountType, req.Value, true, 0
            ));
        });

        adminPromotions.MapGet("/", async (ISender mediator) =>
        {
            return Results.Ok(new[]
            {
                new PromotionDto(Guid.NewGuid(), "SAVE10", "FixedAmount", 10m, true, 5)
            });
        });

        adminPromotions.MapPost("/{id:guid}/deactivate", async (Guid id, ISender mediator) =>
        {
            return Results.NoContent();
        });

        return group;
    }
}
