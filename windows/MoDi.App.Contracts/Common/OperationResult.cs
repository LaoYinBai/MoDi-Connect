namespace MoDi.App.Contracts;

public sealed record OperationResult
{
    private OperationResult(bool isSuccess, string? errorCode, string? userMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        UserMessage = userMessage;
    }

    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? UserMessage { get; }

    public static OperationResult Success(string? message = null) => new(true, null, message);

    public static OperationResult Failure(string errorCode, string userMessage) =>
        new(false, Require(errorCode, nameof(errorCode)), Require(userMessage, nameof(userMessage)));

    internal static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-blank value is required.", parameterName)
            : value;
}
