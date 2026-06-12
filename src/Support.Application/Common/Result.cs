namespace Support.Application.Common;

/// <summary>
/// Semantic failure category — controllers map this to the HTTP status code
/// instead of string-matching error messages.
/// </summary>
public enum ErrorType
{
    None = 0,
    Validation,   // 400
    Unauthorized, // 401
    Forbidden,    // 403
    NotFound,     // 404
    Conflict      // 409
}

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Data { get; private set; }
    public string? ErrorMessage { get; private set; }
    public ErrorType ErrorType { get; private set; } = ErrorType.None;
    public List<string> Errors { get; private set; } = new();

    public static Result<T> Success(T data) => new() { IsSuccess = true, Data = data };

    public static Result<T> Failure(string error, ErrorType errorType = ErrorType.Validation) =>
        new() { IsSuccess = false, ErrorMessage = error, ErrorType = errorType, Errors = new List<string> { error } };

    public static Result<T> Failure(List<string> errors, ErrorType errorType = ErrorType.Validation) =>
        new() { IsSuccess = false, Errors = errors, ErrorType = errorType, ErrorMessage = string.Join("; ", errors) };
}

public class Result
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public ErrorType ErrorType { get; private set; } = ErrorType.None;
    public List<string> Errors { get; private set; } = new();

    public static Result Success() => new() { IsSuccess = true };

    public static Result Failure(string error, ErrorType errorType = ErrorType.Validation) =>
        new() { IsSuccess = false, ErrorMessage = error, ErrorType = errorType, Errors = new List<string> { error } };

    public static Result Failure(List<string> errors, ErrorType errorType = ErrorType.Validation) =>
        new() { IsSuccess = false, Errors = errors, ErrorType = errorType, ErrorMessage = string.Join("; ", errors) };
}
