using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MoDi.Desktop.Platform.Storage;

internal sealed class AtomicJsonStore<T> where T : class
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options;
    private readonly TimeProvider _timeProvider;

    public AtomicJsonStore(
        string filePath,
        TimeProvider timeProvider,
        JsonSerializerOptions? options = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("存储路径不能为空", nameof(filePath))
            : Path.GetFullPath(filePath);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? new JsonSerializerOptions { WriteIndented = true };
    }

    public async Task<T?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            QuarantineCorruptFile();
            return null;
        }
    }

    public async Task WriteAsync(T value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("存储路径没有父目录");
        Directory.CreateDirectory(directory);
        var tempPath = _filePath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private void QuarantineCorruptFile()
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        var baseName = Path.GetFileNameWithoutExtension(_filePath);
        var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var quarantinePath = Path.Combine(directory, $"{baseName}.corrupt-{timestamp}.json");
        File.Move(_filePath, quarantinePath, overwrite: false);
    }
}
