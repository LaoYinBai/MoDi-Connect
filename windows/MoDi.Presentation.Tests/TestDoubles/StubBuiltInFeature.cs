using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class StubBuiltInFeature : IBuiltInFeature
{
    public StubBuiltInFeature(string id = "audio", string displayName = "音频") =>
        Descriptor = new BuiltInFeatureDescriptor(id, displayName, "内置功能", "Music");

    public BuiltInFeatureDescriptor Descriptor { get; }

    public Task<OperationResult> ActivateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(OperationResult.Success());
}
