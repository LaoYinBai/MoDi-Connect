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

    public WindowsLogExportService(ApplicationDataPaths paths, TimeProvider timeProvider)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<OperationResult<LogExportReceipt>> ExportAsync(CancellationToken cancellationToken)
    {
        string? stagingDirectory = null;
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
            var archivePath = Path.Combine(_paths.ExportsDirectory, archiveName);
            if (File.Exists(archivePath))
            {
                archiveName = $"modi-logs-{timestamp}-{Guid.NewGuid():N}.zip";
                archivePath = Path.Combine(_paths.ExportsDirectory, archiveName);
            }
            ZipFile.CreateFromDirectory(stagingDirectory, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return OperationResult<LogExportReceipt>.Success(new LogExportReceipt(archiveName, sourceFiles.Length));
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
        }
    }
}
