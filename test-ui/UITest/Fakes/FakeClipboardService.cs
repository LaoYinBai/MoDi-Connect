using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeClipboardService : IClipboardService
{
    public int CopyCalls { get; private set; }
    public string? LastText { get; private set; }

    public Task<OperationResult> CopyTextAsync(string text, CancellationToken cancellationToken)
    {
        CopyCalls++;
        LastText = text;
        return Task.FromResult(OperationResult.Success());
    }
}
