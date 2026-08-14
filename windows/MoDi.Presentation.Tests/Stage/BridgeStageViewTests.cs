using Avalonia.Controls;
using Avalonia.Threading;
using MoDi.App.Contracts;
using MoDi.Presentation.Stage;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Stage;

[Collection("Avalonia UI")]
public sealed class BridgeStageViewTests
{
    [Fact]
    public void Shared_stage_loads_the_migrated_assets_without_showing_an_error()
    {
        TestApplicationHost.Ensure();
        using var viewModel = new BridgeStageViewModel(
            new RecordingReceiverStatusSource(SnapshotFactory.Receiver(ReceiverState.Connected)),
            new RecordingAppearanceService(AppearanceSnapshot.Default with { ReduceMotion = true }),
            TimeProvider.System);
        var stage = new BridgeStageView { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 400, Content = stage };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(stage.FindControl<Border>("AssetErrorPanel")?.IsVisible);
            Assert.NotNull(stage.FindControl<Image>("BridgeColorImage")?.Source);
            Assert.NotNull(stage.FindControl<Image>("LeftBankImage")?.Source);
        }
        finally
        {
            window.Close();
        }
    }
}
