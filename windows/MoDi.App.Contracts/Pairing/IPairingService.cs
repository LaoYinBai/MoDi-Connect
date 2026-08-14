namespace MoDi.App.Contracts;

public interface IPairingService : IStateSource<PairingSnapshot>, IDisposable
{
    Task<OperationResult> RefreshQrAsync(CancellationToken cancellationToken);
    Task<OperationResult> ConnectAsync(string deviceId, CancellationToken cancellationToken);
}
