using MediatR;
using Vendor.Application.Common.Results;

namespace Vendor.Application.Common.Messaging;

public interface ICommand<TResponse> : IRequest<TResponse>
    where TResponse : IResult;

public interface ICommand : IRequest<Result>;

public interface IQuery<TResponse> : IRequest<TResponse>
    where TResponse : IResult;

public interface IIdempotentRequest
{
    string IdempotencyKey { get; }
}

public interface IIdempotentRequest<TResponse> : IRequest<TResponse>, IIdempotentRequest
    where TResponse : IResult;
