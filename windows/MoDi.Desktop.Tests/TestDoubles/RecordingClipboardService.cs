using MoDi.App.Contracts;

namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class RecordingClipboardService : IClipboardService
{
    public int Calls { get; private set; }
    public string? LastText { get; private set; }

    public Task<OperationResult> CopyTextAsync(string text, CancellationToken cancellationToken)
    {
        Calls++;
        LastText = text;
        return Task.FromResult(OperationResult.Success());
    }
}
