namespace MoDi.App.Contracts;

public interface IClipboardService
{
    Task<OperationResult> CopyTextAsync(string text, CancellationToken cancellationToken);
}
