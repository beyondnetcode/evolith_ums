namespace Evolith.Ums.Sdk.Authorization;

/// <summary>
/// Minimal Result pattern compatible with the codebase's existing Result usage.
/// Consumers with their own Result type can adapt at the call site.
/// </summary>
public readonly struct Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, string? code, string? message)
    {
        IsSuccess = isSuccess;
        ErrorCode = code;
        ErrorMessage = message;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string code, string message) => new(false, code, message);
    public static Result FromDecision(AuthorizationDecision decision) =>
        decision.IsGranted ? Success() : Failure(decision.ErrorCode ?? "AUTH_UNKNOWN", decision.Reason ?? "Denied.");
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value, string? code, string? message)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = code;
        ErrorMessage = message;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string code, string message) => new(false, default, code, message);
    public static Result<T> FromDecision(AuthorizationDecision decision) =>
        decision.IsGranted
            ? throw new InvalidOperationException("Cannot build a failure Result<T> from a Granted decision.")
            : Failure(decision.ErrorCode ?? "AUTH_UNKNOWN", decision.Reason ?? "Denied.");
}
