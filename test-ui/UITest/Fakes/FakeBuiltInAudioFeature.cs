using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeBuiltInAudioFeature : IBuiltInFeature
{
    public BuiltInFeatureDescriptor Descriptor { get; } = new(
        "built-in-audio",
        "音频",
        "内置音频接收模块",
        "Music");

    public int ActivateCalls { get; private set; }

    public Task<OperationResult> ActivateAsync(CancellationToken cancellationToken)
    {
        ActivateCalls++;
        return Task.FromResult(OperationResult.Success());
    }
}
