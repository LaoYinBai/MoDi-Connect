using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeExternalNavigationService : IExternalNavigationService
{
    public int OpenCalls { get; private set; }
    public ExternalDestination? LastDestination { get; private set; }

    public Task<OperationResult> OpenAsync(ExternalDestination destination, CancellationToken cancellationToken)
    {
        OpenCalls++;
        LastDestination = destination;
        return Task.FromResult(OperationResult.Success("仅记录演示目标"));
    }
}
