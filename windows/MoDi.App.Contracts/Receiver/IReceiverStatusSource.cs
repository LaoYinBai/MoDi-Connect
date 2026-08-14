namespace MoDi.App.Contracts;

public interface IReceiverStatusSource : IStateSource<ReceiverSnapshot>, IDisposable
{
    Task<OperationResult> InitializeAsync(CancellationToken cancellationToken);
}
