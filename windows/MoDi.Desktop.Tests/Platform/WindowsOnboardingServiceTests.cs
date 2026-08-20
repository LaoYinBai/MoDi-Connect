using MoDi.App.Contracts;
using MoDi.Desktop.Platform.Onboarding;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class WindowsOnboardingServiceTests
{
    [Fact]
    public async Task First_launch_is_incomplete_and_skip_persists()
    {
        using var temp = TempDirectory.Create();
        var first = await WindowsOnboardingService.CreateAsync(
            new ApplicationDataPaths(temp.Path), TimeProvider.System, [], CancellationToken.None);
        Assert.False(first.Snapshot.IsCompleted);

        Assert.True((await first.SkipAsync(CancellationToken.None)).IsSuccess);
        var reloaded = await WindowsOnboardingService.CreateAsync(
            new ApplicationDataPaths(temp.Path), TimeProvider.System, [], CancellationToken.None);

        Assert.True(reloaded.Snapshot.IsCompleted);
    }

    [Fact]
    public async Task Diagnostics_isolate_probe_failures()
    {
        using var temp = TempDirectory.Create();
        IOnboardingProbe[] probes =
        [
            new DelegateOnboardingProbe("VB_CABLE", _ => Task.FromResult(new DiagnosticResult("VB_CABLE", true, "ok"))),
            new DelegateOnboardingProbe("USB", _ => throw new IOException("probe failed")),
        ];
        var service = await WindowsOnboardingService.CreateAsync(
            new ApplicationDataPaths(temp.Path), TimeProvider.System, probes, CancellationToken.None);

        var result = await service.RunDiagnosticsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Collection(service.Snapshot.Diagnostics,
            item => Assert.True(item.IsSuccess),
            item => Assert.Equal("USB", item.Key));
        Assert.False(service.Snapshot.Diagnostics[1].IsSuccess);
    }
}
