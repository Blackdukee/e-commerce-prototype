using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Vendor.Application.Interfaces;

namespace Vendor.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUserService currentUserService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = currentUserService.UserId ?? "Anonymous";
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Processing request {RequestName} for User {UserId}", requestName, userId);

        var response = await next();

        stopwatch.Stop();
        logger.LogInformation("Handled request {RequestName} in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
