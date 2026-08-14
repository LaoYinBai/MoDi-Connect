using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingAudioSettingsService : IAudioSettingsService
{
    public RecordingAudioSettingsService(AudioSettingsSnapshot? snapshot = null) =>
        Snapshot = snapshot ?? new AudioSettingsSnapshot(0.75, "系统默认播放设备");

    public AudioSettingsSnapshot Snapshot { get; private set; }
    public event Action<AudioSettingsSnapshot>? SnapshotChanged;

    public Task<OperationResult> SetVolumeAsync(double volume, CancellationToken cancellationToken)
    {
        Snapshot = Snapshot with { Volume = volume };
        SnapshotChanged?.Invoke(Snapshot);
        return Task.FromResult(OperationResult.Success());
    }

    public void Dispose()
    {
    }
}
