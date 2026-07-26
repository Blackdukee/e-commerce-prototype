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
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();

            if (response.IsFailure)
            {
                logger.LogWarning("Request {RequestName} failed with error '{ErrorCode}'. Rolling back transaction.",
                    typeof(TRequest).Name, response.Error.Code);
                await unitOfWork.RollbackAsync(cancellationToken);
                return response;
            }

            await unitOfWork.CommitAsync(cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in {RequestName}. Rolling back transaction.", typeof(TRequest).Name);
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
