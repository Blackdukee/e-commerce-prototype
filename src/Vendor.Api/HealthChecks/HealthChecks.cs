using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using Vendor.Infrastructure.Payments;

namespace Vendor.Api.HealthChecks;

public sealed class RedisHealthCheck(IConnectionMultiplexer? redis = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (redis is null || !redis.IsConnected)
        {
            return HealthCheckResult.Healthy("Redis not configured or running in In-Memory mode.");
        }

        try
        {
            var ping = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis is operational (ping: {ping.TotalMilliseconds}ms).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed.", ex);
        }
    }
}

public sealed class PaymentGatewayHealthCheck(IPaymentGatewayFactory gatewayFactory) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var gateway = gatewayFactory.GetPaymentGateway("stripe");
            return Task.FromResult(gateway != null
                ? HealthCheckResult.Healthy("Payment gateway factory resolved successfully.")
                : HealthCheckResult.Degraded("Default stripe payment gateway unresolvable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Payment gateway health check exception.", ex));
        }
    }
}
