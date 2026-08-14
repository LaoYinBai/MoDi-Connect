using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MoDi.Desktop.Platform.Logging;

public sealed class StructuredLogService : IDisposable
{
    private const long DefaultMaxFileBytes = 5L * 1024 * 1024;
    private const long DefaultMaxTotalBytes = 50L * 1024 * 1024;
    private readonly string _logDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly long _maxFileBytes;
    private readonly long _maxTotalBytes;
    private readonly object _gate = new();
    private DateOnly? _activeDate;
    private int _sequence;
    private string? _activeFile;
    private bool _disposed;

    public StructuredLogService(ApplicationDataPaths paths)
        : this(paths?.LogsDirectory ?? throw new ArgumentNullException(nameof(paths)), TimeProvider.System) { }

    internal StructuredLogService(
        string logDirectory,
        TimeProvider timeProvider,
        long maxFileBytes = DefaultMaxFileBytes,
        long maxTotalBytes = DefaultMaxTotalBytes)
    {
        _logDirectory = Path.GetFullPath(logDirectory);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _maxFileBytes = maxFileBytes > 0 ? maxFileBytes : throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        _maxTotalBytes = maxTotalBytes >= maxFileBytes
            ? maxTotalBytes
            : throw new ArgumentOutOfRangeException(nameof(maxTotalBytes));
    }

    public void Write(string level, string tag, string message, Exception? exception = null)
    {
        if (_disposed)
            return;

        try
        {
            var now = _timeProvider.GetUtcNow();
            var entry = new LogEntry(
                now,
                string.IsNullOrWhiteSpace(level) ? "INFO" : level.ToUpperInvariant(),
                LogRedactor.Redact(tag),
                LogRedactor.Redact(message),
                exception is null ? null : LogRedactor.Redact(exception.ToString()));
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            var bytes = Encoding.UTF8.GetBytes(line);

            lock (_gate)
            {
                if (_disposed)
                    return;
                Directory.CreateDirectory(_logDirectory);
                var path = SelectFile(now, bytes.Length);
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
                stream.Flush(flushToDisk: false);
                EnforceTotalBound(path);
            }
        }
        catch
        {
            // Logging must never interrupt receiver/audio execution.
        }
    }

    public void Dispose()
    {
        lock (_gate)
            _disposed = true;
    }

    private string SelectFile(DateTimeOffset now, int incomingBytes)
    {
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        if (_activeDate != date)
        {
            _activeDate = date;
            var prefix = $"modi-{date:yyyyMMdd}-";
            var existing = Directory.GetFiles(_logDirectory, prefix + "*.jsonl")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .LastOrDefault();
            _sequence = existing is null ? 0 : ParseSequence(existing);
            _activeFile = existing ?? BuildPath(date, _sequence);
        }

        if (_activeFile is not null
            && File.Exists(_activeFile)
            && new FileInfo(_activeFile).Length + incomingBytes > _maxFileBytes)
        {
            _sequence++;
            _activeFile = BuildPath(date, _sequence);
        }

        return _activeFile ??= BuildPath(date, _sequence);
    }

    private string BuildPath(DateOnly date, int sequence) =>
        Path.Combine(_logDirectory, $"modi-{date:yyyyMMdd}-{sequence:D3}.jsonl");

    private static int ParseSequence(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(name[(name.LastIndexOf('-') + 1)..], out var value) ? value : 0;
    }

    private void EnforceTotalBound(string activeFile)
    {
        var files = Directory.GetFiles(_logDirectory, "*.jsonl")
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (total <= _maxTotalBytes)
                break;
            if (string.Equals(file.FullName, activeFile, StringComparison.OrdinalIgnoreCase))
                continue;
            total -= file.Length;
            file.Delete();
        }
    }

    internal sealed record LogEntry(
        DateTimeOffset TimestampUtc,
        string Level,
        string Tag,
        string Message,
        string? Exception);
}
