namespace MoDi.App.Contracts;

public sealed record OperationResult<T>
{
    private OperationResult(bool isSuccess, T? value, string? errorCode, string? userMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        UserMessage = userMessage;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? UserMessage { get; }

    public static OperationResult<T> Success(T value, string? message = null) =>
        new(true, value, null, message);

    public static OperationResult<T> Failure(string errorCode, string userMessage) =>
        new(false, default, OperationResult.Require(errorCode, nameof(errorCode)),
            OperationResult.Require(userMessage, nameof(userMessage)));
}
