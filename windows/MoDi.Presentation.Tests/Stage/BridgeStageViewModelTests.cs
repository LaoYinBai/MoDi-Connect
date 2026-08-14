using MoDi.App.Contracts;
using MoDi.Presentation.Stage;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Stage;

public sealed class BridgeStageViewModelTests
{
    [Theory]
    [InlineData(ReceiverState.Idle, StageConnectionState.Disconnected)]
    [InlineData(ReceiverState.Searching, StageConnectionState.Handshaking)]
    [InlineData(ReceiverState.Connecting, StageConnectionState.Handshaking)]
    [InlineData(ReceiverState.Connected, StageConnectionState.Connected)]
    [InlineData(ReceiverState.Streaming, StageConnectionState.Connected)]
    [InlineData(ReceiverState.Reconnecting, StageConnectionState.Reconnecting)]
    [InlineData(ReceiverState.Error, StageConnectionState.Disconnected)]
    public void Receiver_state_maps_to_the_stage_vocabulary(
        ReceiverState receiverState,
        StageConnectionState expected)
    {
        var receiver = new RecordingReceiverStatusSource(SnapshotFactory.Receiver(receiverState));
        using var viewModel = new BridgeStageViewModel(
            receiver,
            new RecordingAppearanceService(),
            TimeProvider.System);

        Assert.Equal(expected, viewModel.State);
    }

    [Fact]
    public void Reduced_motion_uses_the_settled_frame_immediately()
    {
        var receiver = new RecordingReceiverStatusSource(
            SnapshotFactory.Receiver(ReceiverState.Connected, rms: 0.72));
        var appearance = new RecordingAppearanceService(
            AppearanceSnapshot.Default with { ReduceMotion = true });
        using var viewModel = new BridgeStageViewModel(receiver, appearance, TimeProvider.System);

        Assert.Equal(StageConnectionState.Connected, viewModel.State);
        Assert.Equal(1d, viewModel.Frame.BoyTravel);
        Assert.Equal(1d, viewModel.Frame.ColorReveal);
        Assert.Equal(1d, viewModel.Frame.WaterLevel);
        Assert.True(viewModel.Frame.RmsEnabled);
        Assert.Equal(0.72, viewModel.Rms);
    }

    [Fact]
    public void Dispose_stops_receiver_updates_from_reaching_the_stage()
    {
        var receiver = new RecordingReceiverStatusSource();
        var viewModel = new BridgeStageViewModel(
            receiver,
            new RecordingAppearanceService(),
            TimeProvider.System);
        viewModel.Dispose();

        receiver.Publish(SnapshotFactory.Receiver(ReceiverState.Connected));

        Assert.Equal(StageConnectionState.Disconnected, viewModel.State);
    }
}
