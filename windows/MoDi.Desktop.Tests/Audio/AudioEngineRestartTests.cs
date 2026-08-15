using MoDi.Desktop;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Audio;

/// <summary>
/// 重连无声回归测试：AudioEngine.Stop() 会停�?Router 的全部输出，
/// Start() 必须恢复当前模式的输出，否则重连后解码正常但无声�?/// </summary>
public sealed class AudioEngineRestartTests
{
    [Fact]
    public void Start_after_stop_restores_speaker_output()
    {
        var speaker = new RecordingAudioRenderer();
        var cable = new RecordingAudioRenderer();
        var transport = new LoopbackTransport();
        using var engine = new AudioEngine(transport, speaker, cable);

        engine.Start();
        var playsAfterFirstStart = speaker.PlayCount;
        Assert.True(playsAfterFirstStart >= 1);

        engine.Stop();

        engine.Start();

        Assert.True(speaker.PlayCount > playsAfterFirstStart);
    }

    [Fact]
    public void Start_after_stop_restores_output_for_the_current_mode()
    {
        var speaker = new RecordingAudioRenderer();
        var cable = new RecordingAudioRenderer();
        var transport = new LoopbackTransport();
        using var engine = new AudioEngine(transport, speaker, cable);

        engine.Start();
        Assert.True(engine.Router.SetMode(AudioRouter.RouteMode.MicOnly));
        var cablePlaysAfterSetMode = cable.PlayCount;
        var speakerPlaysAfterSetMode = speaker.PlayCount;

        engine.Stop();
        engine.Start();

        Assert.True(cable.PlayCount > cablePlaysAfterSetMode);
        Assert.Equal(speakerPlaysAfterSetMode, speaker.PlayCount);
    }

    [Fact]
    public void Start_without_prior_prepare_does_not_touch_devices_beyond_null_safe_calls()
    {
        var speaker = new RecordingAudioRenderer();
        var cable = new RecordingAudioRenderer();
        var transport = new LoopbackTransport();
        using var engine = new AudioEngine(transport, speaker, cable);

        engine.Start();

        Assert.False(speaker.IsReady);
        Assert.False(cable.IsReady);
        Assert.True(transport.IsConnected);
    }
}
