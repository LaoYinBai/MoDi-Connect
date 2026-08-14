using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Features;

public sealed class BuiltInAudioFeature(Action? activate = null) : IBuiltInFeature
{
    private readonly Action _activate = activate ?? (() => { });

    public BuiltInFeatureDescriptor Descriptor { get; } = new(
        "audio",
        "音频",
        "内置功能",
        "MusicNote");

    public Task<OperationResult> ActivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _activate();
        return Task.FromResult(OperationResult.Success());
    }
}
