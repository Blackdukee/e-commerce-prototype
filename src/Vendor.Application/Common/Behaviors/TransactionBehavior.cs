using MediatR;
using Microsoft.Extensions.Logging;
using Vendor.Application.Common.Messaging;
using Vendor.Application.Common.Results;
using Vendor.Application.Interfaces;

namespace Vendor.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
    where TResponse : IResult
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var response = await next();

            if (response.IsFailure)
            {
                logger.LogWarning("Request {RequestName} failed with error '{ErrorCode}'. Rolling back transaction.",
                    typeof(TRequest).Name, response.Error.Code);
            }

            return response;
        }, cancellationToken);
    }
}
