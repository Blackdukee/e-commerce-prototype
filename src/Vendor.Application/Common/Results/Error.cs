namespace Vendor.Application.Common.Results;

public enum ErrorType
{
    Failure = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,
    Validation = 422
}

public record Error(string Code, string Description, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error NotFound(string entityName, object key) =>
        new NotFoundError(entityName, key);

    public static Error Validation(IDictionary<string, string[]> errors) =>
        new ValidationError(errors);

    public static Error Conflict(string code, string description) =>
        new ConflictError(code, description);

    public static Error Unauthorized(string description = "Unauthorized access.") =>
        new UnauthorizedError(description);

    public static Error Forbidden(string description = "Forbidden access.") =>
        new ForbiddenError("Auth.Forbidden", description);

    public static Error Forbidden(string code, string description) =>
        new ForbiddenError(code, description);
}

public sealed record NotFoundError(string EntityName, object Key)
    : Error($"{EntityName}.NotFound", $"Entity '{EntityName}' with key '{Key}' was not found.", ErrorType.NotFound);

public sealed record ValidationError(IDictionary<string, string[]> Errors)
    : Error("Validation.Failure", "One or more validation errors occurred.", ErrorType.Validation);

public sealed record ConflictError(string Code, string Description)
    : Error(Code, Description, ErrorType.Conflict);

public sealed record UnauthorizedError(string Description)
    : Error("Auth.Unauthorized", Description, ErrorType.Unauthorized);

public sealed record ForbiddenError(string Code, string Description)
    : Error(Code, Description, ErrorType.Forbidden)
{
    public ForbiddenError(string description) : this("Auth.Forbidden", description) { }
}
