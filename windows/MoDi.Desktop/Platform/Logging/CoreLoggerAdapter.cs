using System;
using System.Threading;
using MoDi.Core;

namespace MoDi.Desktop.Platform.Logging;

public sealed class CoreLoggerAdapter(StructuredLogService writer) : ILogger
{
    private static readonly AsyncLocal<ConnectivityLogContext?> CurrentContext = new();
    private readonly StructuredLogService _writer =
        writer ?? throw new ArgumentNullException(nameof(writer));

    public void Debug(string tag, string msg) => _writer.Write("DEBUG", tag, msg, context: CurrentContext.Value);
    public void Info(string tag, string msg) => _writer.Write("INFO", tag, msg, context: CurrentContext.Value);
    public void Warn(string tag, string msg) => _writer.Write("WARN", tag, msg, context: CurrentContext.Value);
    public void Error(string tag, string msg) => _writer.Write("ERROR", tag, msg, context: CurrentContext.Value);
    public void Error(string tag, string msg, Exception ex) => _writer.Write("ERROR", tag, msg, ex, CurrentContext.Value);

    public static IDisposable BeginContext(ConnectivityLogContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = CurrentContext.Value;
        CurrentContext.Value = context;
        return new ContextScope(previous);
    }

    private sealed class ContextScope(ConnectivityLogContext? previous) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentContext.Value = previous;
        }
    }
}
