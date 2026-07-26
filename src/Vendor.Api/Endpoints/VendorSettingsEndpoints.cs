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

        group.MapGet("/schema", async () =>
        {
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "config", "vendor.config.schema.json");
            if (!File.Exists(schemaPath))
            {
                schemaPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "vendor.config.schema.json");
            }

            if (!File.Exists(schemaPath))
            {
                return Results.NotFound(new { error = "JSON Schema file not found." });
            }

            var schemaContent = await File.ReadAllTextAsync(schemaPath);
            return Results.Content(schemaContent, "application/schema+json");
        })
        .WithName("GetVendorConfigSchema")
        .Produces(StatusCodes.Status200OK);

        return routes;
    }
}
