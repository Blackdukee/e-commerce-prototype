using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Application.Common.Interfaces;

namespace Vendor.Api.Endpoints;

public static class MediaEndpoints
{
    public static RouteGroupBuilder MapMediaEndpoints(this RouteGroupBuilder group)
    {
        var media = group.MapGroup("/media").WithTags("Media");

        media.MapGet("/presigned-url", async (string? fileName, string? contentType, int? expirationMinutes, IFileStorageService storageService, CancellationToken ct) =>

        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Results.BadRequest(new { Error = "fileName query parameter is required." });
            }

            var effectiveContentType = !string.IsNullOrWhiteSpace(contentType) ? contentType : "application/octet-stream";
            var expiration = TimeSpan.FromMinutes(expirationMinutes is > 0 ? expirationMinutes.Value : 15);

            var uploadUrl = await storageService.GeneratePresignedUploadUrlAsync(fileName, effectiveContentType, expiration, ct);
            return Results.Ok(new { Url = uploadUrl, FileName = fileName });
        }).RequireAuthorization();

        return group;
    }
}
