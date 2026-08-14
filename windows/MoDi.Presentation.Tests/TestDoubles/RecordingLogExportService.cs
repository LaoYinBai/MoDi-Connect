using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingLogExportService : ILogExportService
{
    public OperationResult<LogExportReceipt> Result { get; set; } =
        OperationResult<LogExportReceipt>.Success(new LogExportReceipt("MoDi-test-logs.zip", 3));
    public int ExportCalls { get; private set; }

    public Task<OperationResult<LogExportReceipt>> ExportAsync(CancellationToken cancellationToken)
    {
        ExportCalls++;
        return Task.FromResult(Result);
    }
}
