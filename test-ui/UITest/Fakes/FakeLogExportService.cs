using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeLogExportService : ILogExportService
{
    public int ExportCalls { get; private set; }

    public Task<OperationResult<LogExportReceipt>> ExportAsync(CancellationToken cancellationToken)
    {
        ExportCalls++;
        return Task.FromResult(OperationResult<LogExportReceipt>.Success(
            new LogExportReceipt("MoDi-test-logs.zip", 3)));
    }
}
