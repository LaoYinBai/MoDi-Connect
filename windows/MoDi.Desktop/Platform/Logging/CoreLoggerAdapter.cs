using System;
using MoDi.Core;

namespace MoDi.Desktop.Platform.Logging;

public sealed class CoreLoggerAdapter(StructuredLogService writer) : ILogger
{
    private readonly StructuredLogService _writer =
        writer ?? throw new ArgumentNullException(nameof(writer));

    public void Debug(string tag, string msg) => _writer.Write("DEBUG", tag, msg);
    public void Info(string tag, string msg) => _writer.Write("INFO", tag, msg);
    public void Warn(string tag, string msg) => _writer.Write("WARN", tag, msg);
    public void Error(string tag, string msg) => _writer.Write("ERROR", tag, msg);
    public void Error(string tag, string msg, Exception ex) => _writer.Write("ERROR", tag, msg, ex);
}
