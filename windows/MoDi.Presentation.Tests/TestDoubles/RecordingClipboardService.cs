using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingClipboardService : IClipboardService
{
    public OperationResult Result { get; set; } = OperationResult.Success();
    public int CopyCalls { get; private set; }
    public string? LastText { get; private set; }

    public Task<OperationResult> CopyTextAsync(string text, CancellationToken cancellationToken)
    {
        CopyCalls++;
        LastText = text;
        return Task.FromResult(Result);
    }
}
