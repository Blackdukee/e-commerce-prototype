using Microsoft.AspNetCore.Http;
using Vendor.Application.Common.Results;
using HttpIResult = Microsoft.AspNetCore.Http.IResult;

namespace Vendor.Api.Extensions;

public static class ResultExtensions
{
    public static HttpIResult ToHttpResult(this Result result, HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return CreateProblemDetails(result.Error, httpContext);
    }

    public static HttpIResult ToHttpResult<T>(this Result<T> result, HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return CreateProblemDetails(result.Error, httpContext);
    }

    public static HttpIResult ToCreatedHttpResult<T>(this Result<T> result, string uri, HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return Results.Created(uri, result.Value);
        }

        return CreateProblemDetails(result.Error, httpContext);
    }

    private static HttpIResult CreateProblemDetails(Error error, HttpContext? httpContext)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        var title = error.Type switch
        {
            ErrorType.Validation => "Unprocessable Entity",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            _ => "Bad Request"
        };

        var extensions = new Dictionary<string, object?>();
        if (httpContext?.Items["CorrelationId"] is string correlationId)
        {
            extensions["correlationId"] = correlationId;
        }

        if (error is ValidationError validationError && validationError.Errors?.Count > 0)
        {
            extensions["errors"] = validationError.Errors;
        }

        return Results.Problem(
            detail: error.Description,
            instance: httpContext?.Request.Path,
            statusCode: statusCode,
            title: title,
            type: $"https://httpstatuses.com/{statusCode}",
            extensions: extensions
        );
    }
}
