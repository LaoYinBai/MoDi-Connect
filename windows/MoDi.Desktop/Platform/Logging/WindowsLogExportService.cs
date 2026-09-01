using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Logging;

public sealed class WindowsLogExportService : ILogExportService
{
    private readonly ApplicationDataPaths _paths;
    private readonly TimeProvider _timeProvider;
    private readonly ILogArchiveSaveService _archiveSaveService;

    public WindowsLogExportService(
        ApplicationDataPaths paths,
        TimeProvider timeProvider,
        ILogArchiveSaveService archiveSaveService)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _archiveSaveService = archiveSaveService ?? throw new ArgumentNullException(nameof(archiveSaveService));
    }

    public async Task<OperationResult<LogExportReceipt>> ExportAsync(CancellationToken cancellationToken)
    {
        string? stagingDirectory = null;
        string? temporaryArchive = null;
        try
        {
            Directory.CreateDirectory(_paths.ExportsDirectory);
            stagingDirectory = Path.Combine(_paths.ExportsDirectory, ".staging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);

            var sourceFiles = Directory.Exists(_paths.LogsDirectory)
                ? Directory.GetFiles(_paths.LogsDirectory, "*.jsonl", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            foreach (var source in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = await File.ReadAllTextAsync(source, cancellationToken).ConfigureAwait(false);
                var target = Path.Combine(stagingDirectory, Path.GetFileName(source));
                await File.WriteAllTextAsync(
                    target,
                    LogRedactor.Redact(content),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken).ConfigureAwait(false);
            }

            var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
            var archiveName = $"modi-logs-{timestamp}.zip";
            temporaryArchive = Path.Combine(_paths.ExportsDirectory, ".archive-" + Guid.NewGuid().ToString("N") + ".zip");
            ZipFile.CreateFromDirectory(stagingDirectory, temporaryArchive, CompressionLevel.Optimal, includeBaseDirectory: false);

            var saved = await _archiveSaveService.SaveAsync(archiveName, temporaryArchive, cancellationToken);
            if (!saved.IsSuccess || string.IsNullOrWhiteSpace(saved.Value))
            {
                return OperationResult<LogExportReceipt>.Failure(
                    saved.ErrorCode ?? "LOG_EXPORT_SAVE",
                    saved.UserMessage ?? "未能保存日志压缩包");
            }

            return OperationResult<LogExportReceipt>.Success(
                new LogExportReceipt(Path.GetFileName(saved.Value), sourceFiles.Length));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return OperationResult<LogExportReceipt>.Failure("LOG_EXPORT", $"导出日志失败：{ex.Message}");
        }
        finally
        {
            if (stagingDirectory is not null)
            {
                try
                {
                    if (Directory.Exists(stagingDirectory))
                        Directory.Delete(stagingDirectory, recursive: true);
                }
                catch
                {
                    // A failed cleanup must not replace the export result.
                }
            }
            if (temporaryArchive is not null)
            {
                try
                {
                    if (File.Exists(temporaryArchive))
                        File.Delete(temporaryArchive);
                }
                catch
                {
                    // A failed cleanup must not replace the export result.
                }
            }
        }
    }
}
