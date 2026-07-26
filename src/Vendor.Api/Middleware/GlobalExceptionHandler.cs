using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vendor.Domain.Exceptions;

namespace Vendor.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Validation Error",
                "One or more validation failures occurred.",
                valEx.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ),
            BusinessRuleViolationException ruleEx => (
                StatusCodes.Status409Conflict,
                "Business Rule Violation",
                ruleEx.Message,
                null as Dictionary<string, string[]>
            ),
            DomainException domainEx => (
                StatusCodes.Status400BadRequest,
                "Domain Error",
                domainEx.Message,
                null as Dictionary<string, string[]>
            ),
            KeyNotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                notFoundEx.Message,
                null as Dictionary<string, string[]>
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication credentials are missing or invalid.",
                null as Dictionary<string, string[]>
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                env.IsDevelopment() ? exception.ToString() : "An unexpected internal error occurred. Please contact support with the correlation ID.",
                null as Dictionary<string, string[]>
            )
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
