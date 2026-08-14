using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeAudioSettingsService : IAudioSettingsService
{
    public AudioSettingsSnapshot Snapshot { get; private set; } = new(0.72, "系统默认播放设备");
    public int SetVolumeCalls { get; private set; }
    public event Action<AudioSettingsSnapshot>? SnapshotChanged;

    public Task<OperationResult> SetVolumeAsync(double volume, CancellationToken cancellationToken)
    {
        SetVolumeCalls++;
        Publish(Snapshot with { Volume = Math.Clamp(volume, 0, 1) });
        return Task.FromResult(OperationResult.Success());
    }

    public void Publish(AudioSettingsSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }

    public void Dispose()
    {
    }
}
