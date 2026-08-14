using System;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Stage;
using UITest.Demo;
using Xunit;

namespace UITest.Tests.Harness;

public sealed class TestUiCompositionTests
{
    [Fact]
    public void Demo_receiver_commands_publish_only_in_memory_snapshots()
    {
        using var composition = new TestUiComposition(TimeProvider.System);

        composition.Demo.SetReceiverStateCommand.Execute("connected");

        Assert.Equal(ReceiverState.Connected, composition.Receiver.Snapshot.State);
        Assert.Equal(StageConnectionState.Connected, composition.Shell.Stage.State);
    }

    [Fact]
    public async Task Demo_qr_refresh_delegates_to_the_fake_pairing_service()
    {
        using var composition = new TestUiComposition(TimeProvider.System);

        await composition.Shell.QrPairing.RefreshCommand.ExecuteAsync();

        Assert.Equal(1, composition.Pairing.RefreshCalls);
        Assert.False(composition.Pairing.Snapshot.QrPng.IsEmpty);
    }

    [Theory]
    [InlineData("empty", 0)]
    [InlineData("healthy", 1)]
    [InlineData("disabled", 1)]
    [InlineData("incompatible", 1)]
    [InlineData("crashed", 1)]
    [InlineData("loading", 1)]
    public void Plugin_demo_exposes_all_six_visual_states(string scenario, int count)
    {
        using var composition = new TestUiComposition(TimeProvider.System);

        composition.Demo.SetPluginScenarioCommand.Execute(scenario);

        Assert.Equal(count, composition.Plugins.Snapshot.Entries.Count);
    }

    [Fact]
    public void Navigation_uses_the_shared_shell_view_models()
    {
        using var composition = new TestUiComposition(TimeProvider.System);

        composition.Shell.Navigation.NavigateCommand.Execute(AppPage.Settings);
        Assert.Same(composition.Shell.Settings, composition.Shell.CurrentPageViewModel);

        composition.Shell.Navigation.NavigateCommand.Execute(AppPage.About);
        Assert.Same(composition.Shell.About, composition.Shell.CurrentPageViewModel);
    }
}
