using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeImageSelectionService : IImageSelectionService
{
    private static readonly byte[] DemoImage = [137, 80, 78, 71, 13, 10, 26, 10];
    public int SelectCalls { get; private set; }

    public Task<OperationResult<SelectedImage>> SelectImageAsync(CancellationToken cancellationToken)
    {
        SelectCalls++;
        return Task.FromResult(OperationResult<SelectedImage>.Success(
            new SelectedImage("river-bank-demo.png", DemoImage)));
    }
}
