using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Logging;

public interface ILogArchiveSaveService
{
    Task<OperationResult<string>> SaveAsync(
        string suggestedFileName,
        string sourceArchivePath,
        CancellationToken cancellationToken);
}
