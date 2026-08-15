using MoDi.Core;
using MoDi.Desktop;

namespace MoDi.Desktop.Tests.TestDoubles;

/// <summary>
/// 测试用 IAudioRenderer 替身：不触碰音频设备，只记录 Play/Stop 调用次数。
/// </summary>
public sealed class RecordingAudioRenderer : IAudioRenderer
{
    private bool _prepared;

    public int PlayCount { get; private set; }

    public int StopCount { get; private set; }

    public bool IsReady => _prepared;

    public bool Prepare(AudioConfig config)
    {
        _prepared = true;
        return true;
    }

    public void Play() => PlayCount++;

    public void Stop() => StopCount++;

    public void SetVolume(float volume)
    {
    }

    public void Mute(bool muted)
    {
    }

    public void FeedPcm(byte[] data, int offset, int count)
    {
    }

    public void Release()
    {
        _prepared = false;
    }
}
