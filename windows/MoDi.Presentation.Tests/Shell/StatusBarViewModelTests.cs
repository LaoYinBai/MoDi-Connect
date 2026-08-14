using MoDi.App.Contracts;
using MoDi.Presentation.Shell;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Shell;

public sealed class StatusBarViewModelTests
{
    [Fact]
    public void No_active_session_is_rendered_as_none()
    {
        var receiver = new RecordingReceiverStatusSource(
            SnapshotFactory.Receiver() with { ActiveLink = LinkKind.None });
        var audio = new RecordingAudioSettingsService();
        using var viewModel = new StatusBarViewModel(receiver, audio);

        Assert.Equal("当前链路 无 · 当前输出 系统默认播放设备", viewModel.StatusMessage);
    }
}
