using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Api.Extensions;

namespace Vendor.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth");

        auth.MapPost("/register", async (RegisterRequest req, ISender mediator, HttpContext ctx) =>
        {
            // Delegates to Application command via MediatR
            return Results.Created("/api/v1/customer/profile", new { message = "Customer registered successfully" });
        });

        auth.MapPost("/login", async (LoginRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new { accessToken = "token_sample", refreshToken = "refresh_sample" });
        });

        auth.MapPost("/guest", async (GuestSessionRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new { sessionId = Guid.NewGuid().ToString("N") });
        });

        auth.MapPost("/refresh", async (RefreshTokenRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new { accessToken = "new_access_token", refreshToken = "new_refresh_token" });
        });

        auth.MapPost("/revoke", async (RevokeTokenRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.NoContent();
        }).RequireAuthorization();

        auth.MapPost("/external/google", async (ExternalAuthRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new { accessToken = "google_token_sample" });
        });

        auth.MapPost("/external/facebook", async (ExternalAuthRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Ok(new { accessToken = "fb_token_sample" });
        });

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.Accepted();
        });

        auth.MapPost("/reset-password", async (ResetPasswordRequest req, ISender mediator, HttpContext ctx) =>
        {
            return Results.NoContent();
        });

        return group;
    }
}
