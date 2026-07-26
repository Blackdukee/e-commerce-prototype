using MediatR;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;

namespace Vendor.Application.Common.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyStore idempotencyStore)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentRequest<TResponse>
    where TResponse : IResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return await next();
        }

        var cachedResponse = await idempotencyStore.GetResultAsync<TResponse>(
            request.IdempotencyKey, cancellationToken);

        if (cachedResponse is not null)
        {
            return cachedResponse;
        }

        var response = await next();

        if (response.IsSuccess)
        {
            await idempotencyStore.SaveResultAsync(
                request.IdempotencyKey, response, cancellationToken);
        }

        return response;
    }
}
