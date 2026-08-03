using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vendor.Api.DTOs;
using Vendor.Api.Extensions;
using Vendor.Application.Modules.Auth;

namespace Vendor.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth-policy");

        auth.MapPost("/register", async (RegisterRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new RegisterCustomerCommand(req.Email, req.Password, req.FirstName, req.LastName);
            var result = await mediator.Send(command);
            return result.ToCreatedHttpResult("/api/v1/customer/profile", ctx);
        });

        auth.MapPost("/login", async (LoginRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new LoginWithPasswordCommand(req.Email, req.Password);
            var result = await mediator.Send(command);
            return result.ToHttpResult(ctx);
        });

        auth.MapPost("/guest", async (GuestSessionRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new CreateGuestSessionCommand(req?.SessionId);
            var result = await mediator.Send(command);
            return result.ToHttpResult(ctx);
        });

        auth.MapPost("/refresh", async (RefreshTokenRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new RefreshTokenCommand(req.RefreshToken);
            var result = await mediator.Send(command);
            return result.ToHttpResult(ctx);
        });

        auth.MapPost("/revoke", async (RevokeTokenRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new RevokeTokenCommand(req.RefreshToken);
            var result = await mediator.Send(command);
            return result.ToHttpResult(ctx);
        }).RequireAuthorization();

        auth.MapPost("/external/google", async (ExternalAuthRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new LoginWithOAuthCommand("google", req.IdToken);
            var result = await mediator.Send(command);
            return result.ToHttpResult(ctx);
        });

        auth.MapPost("/external/facebook", async (ExternalAuthRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new LoginWithOAuthCommand("facebook", req.IdToken);
            var result = await mediator.Send(command);
            return result.ToHttpResult(ctx);
        });

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new ForgotPasswordCommand(req.Email);
            var result = await mediator.Send(command);
            return result.IsSuccess ? Results.Accepted() : result.ToHttpResult(ctx);
        });

        auth.MapPost("/reset-password", async (ResetPasswordRequest req, ISender mediator, HttpContext ctx) =>
        {
            var command = new ResetPasswordCommand(req.Email, req.Token, req.NewPassword);
            var result = await mediator.Send(command);
            return result.ToHttpResult(ctx);
        });

        return group;
    }
}
