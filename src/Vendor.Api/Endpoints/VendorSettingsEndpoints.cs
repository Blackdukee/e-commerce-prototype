using System;
using System.IO;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Application.Commands.VendorSettings;
using Vendor.Application.DTOs;
using Vendor.Application.Queries.VendorSettings;

namespace Vendor.Api.Endpoints;

public static class VendorSettingsEndpoints
{
    public static IEndpointRouteBuilder MapVendorSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin/config")
            .WithTags("Vendor Configuration Admin");

        group.MapGet("/", async (ISender mediator) =>
        {
            var result = await mediator.Send(new GetVendorConfigQuery("acme-store"));
            return Results.Ok(result);
        })
        .WithName("GetVendorConfig")
        .RequireAuthorization()
        .Produces<VendorConfigDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/", async (VendorConfigPatchDto patch, ISender mediator, HttpContext context) =>
        {
            if (patch == null || patch.Runtime == null)
            {
                return Results.BadRequest(new { error = "Runtime tier patch content is required." });
            }

            var modifiedBy = context.User?.Identity?.Name ?? "admin";
            var command = new UpdateVendorSettingsCommand("acme-store", patch.Runtime, patch.Version, modifiedBy);

            try
            {
                var updated = await mediator.Send(command);
                return Results.Ok(updated);
            }
            catch (FluentValidation.ValidationException valEx)
            {
                return Results.BadRequest(new { errors = valEx.Errors });
            }
            catch (Exception ex) when (ex.Message.Contains("Concurrency") || ex.Message.Contains("concurrency"))
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .WithName("UpdateVendorConfig")
        .RequireAuthorization()
        .Produces<VendorConfigDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict);

        return group;
    }
}
