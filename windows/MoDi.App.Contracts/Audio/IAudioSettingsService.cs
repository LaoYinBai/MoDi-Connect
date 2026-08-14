namespace MoDi.App.Contracts;

public interface IAudioSettingsService : IStateSource<AudioSettingsSnapshot>, IDisposable
{
    Task<OperationResult> SetVolumeAsync(double volume, CancellationToken cancellationToken);
}
