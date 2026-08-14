using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakePersonalizationResetService(FakeAppearanceService appearance) : IPersonalizationResetService
{
    public int ResetCalls { get; private set; }

    public Task<OperationResult> ResetAsync(CancellationToken cancellationToken)
    {
        ResetCalls++;
        appearance.Reset();
        return Task.FromResult(OperationResult.Success());
    }
}
