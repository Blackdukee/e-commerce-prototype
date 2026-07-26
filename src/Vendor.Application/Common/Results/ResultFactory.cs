namespace Vendor.Application.Common.Results;

public static class ResultFactory
{
    public static TResponse CreateFailure<TResponse>(Error error)
    {
        var targetType = typeof(TResponse);

        if (targetType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = targetType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure), [typeof(Error)]);

            return (TResponse)failureMethod!.Invoke(null, [error])!;
        }

        throw new InvalidOperationException($"Type '{targetType.Name}' is not a supported Result type.");
    }
}

public static class ResultExtensions
{
    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure) =>
        result.IsSuccess ? onSuccess() : onFailure(result.Error);
}
