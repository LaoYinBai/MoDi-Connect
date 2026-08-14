namespace MoDi.App.Contracts;

public interface ILogExportService
{
    Task<OperationResult<LogExportReceipt>> ExportAsync(CancellationToken cancellationToken);
}
