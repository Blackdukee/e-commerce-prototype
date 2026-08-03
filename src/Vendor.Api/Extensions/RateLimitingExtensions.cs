using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Vendor.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth-policy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ResolveClientIp(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy("cart-checkout-policy", httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: ResolveClientIp(httpContext),
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 30,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 30,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static string ResolveClientIp(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var ip = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        if (httpContext.Request.Headers.TryGetValue("X-Real-IP", out var realIp) &&
            !string.IsNullOrWhiteSpace(realIp))
        {
            var ip = realIp.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
