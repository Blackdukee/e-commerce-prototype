using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Vendor.Application.Common.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var response = await next();
        timer.Stop();

        if (timer.ElapsedMilliseconds > 500)
        {
            logger.LogWarning(
                "Long Running Request Warning: {Name} ({ElapsedMilliseconds} ms) {@Request}",
                typeof(TRequest).Name, timer.ElapsedMilliseconds, request);
        }

        return response;
    }
}
