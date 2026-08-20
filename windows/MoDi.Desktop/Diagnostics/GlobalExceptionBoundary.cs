using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop.Diagnostics;

public sealed class GlobalExceptionBoundary : IDisposable
{
    private const string Tag = "GlobalExceptionBoundary";
    private static readonly object ProcessGate = new();
    private static GlobalExceptionBoundary? _processBoundary;

    private readonly IProcessExceptionHooks _hooks;
    private readonly Action<GlobalExceptionRecord> _sink;
    private readonly object _gate = new();
    private bool _installed;
    private bool _disposed;

    private GlobalExceptionBoundary(
        IProcessExceptionHooks hooks,
        Action<GlobalExceptionRecord> sink)
    {
        _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public static GlobalExceptionBoundary Install()
    {
        lock (ProcessGate)
        {
            if (_processBoundary is null || _processBoundary._disposed)
                _processBoundary = new GlobalExceptionBoundary(ProcessExceptionHooks.Instance, WriteToLog);

            _processBoundary.InstallCore();
            return _processBoundary;
        }
    }

    public static void Observe(Task task, string operationName) =>
        ObserveForTest(task, operationName, WriteToLog);

    internal static InitializationFailure ReportInitializationFailure(Exception exception) =>
        ReportInitializationFailureForTest(exception, WriteToLog);

    internal static bool HandleDispatcherException(Exception exception) =>
        HandleDispatcherExceptionForTest(exception, WriteToLog);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_installed)
            {
                _hooks.UnsubscribeAppDomain(OnAppDomainUnhandledException);
                _hooks.UnsubscribeTaskScheduler(OnUnobservedTaskException);
                _installed = false;
            }
        }

        lock (ProcessGate)
        {
            if (ReferenceEquals(_processBoundary, this))
                _processBoundary = null;
        }
    }

    internal static GlobalExceptionBoundary CreateForTest(
        IProcessExceptionHooks hooks,
        Action<GlobalExceptionRecord> sink) => new(hooks, sink);

    internal void InstallForTest() => InstallCore();

    internal static void ObserveForTest(
        Task task,
        string operationName,
        Action<GlobalExceptionRecord> sink)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(sink);

        _ = task.ContinueWith(
            completed => RecordTaskFailure(completed.Exception!, operationName, sink),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal static bool HandleDispatcherExceptionForTest(
        Exception exception,
        Action<GlobalExceptionRecord> sink)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(sink);

        if (exception is CommandFailureException)
        {
            SafeRecord(sink, new GlobalExceptionRecord("Avalonia.CommandFailure", exception));
            return true;
        }

        SafeRecord(sink, new GlobalExceptionRecord("Avalonia.DispatcherUnhandledException", exception));
        return false;
    }

    internal static InitializationFailure ReportInitializationFailureForTest(
        Exception exception,
        Action<GlobalExceptionRecord> sink)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(sink);

        SafeRecord(sink, new GlobalExceptionRecord("APP_INITIALIZE_FAILED", exception));
        return new InitializationFailure(
            "APP_INITIALIZE_FAILED",
            "应用初始化失败，请查看日志并重新启动。");
    }

    private void InstallCore()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_installed)
                return;

            _hooks.SubscribeAppDomain(OnAppDomainUnhandledException);
            _hooks.SubscribeTaskScheduler(OnUnobservedTaskException);
            _installed = true;
        }
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        var exception = eventArgs.ExceptionObject as Exception
            ?? new InvalidOperationException($"Unhandled non-exception object: {eventArgs.ExceptionObject}");
        SafeRecord(
            _sink,
            new GlobalExceptionRecord(
                "AppDomain.UnhandledException",
                exception,
                eventArgs.IsTerminating));
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        try
        {
            RecordTaskFailure(
                eventArgs.Exception,
                "TaskScheduler.UnobservedTaskException",
                _sink);
        }
        finally
        {
            eventArgs.SetObserved();
        }
    }

    private static void RecordTaskFailure(
        AggregateException aggregate,
        string source,
        Action<GlobalExceptionRecord> sink)
    {
        foreach (var exception in aggregate.Flatten().InnerExceptions)
            SafeRecord(sink, new GlobalExceptionRecord(source, exception));
    }

    private static void SafeRecord(
        Action<GlobalExceptionRecord> sink,
        GlobalExceptionRecord item)
    {
        try
        {
            sink(item);
        }
        catch
        {
            // Exception reporting must never replace the original failure.
        }
    }

    private static void WriteToLog(GlobalExceptionRecord item) =>
        Log.E(
            Tag,
            $"Source={item.Source}; IsTerminating={item.IsTerminating?.ToString() ?? "n/a"}",
            item.Exception);
}

internal sealed record GlobalExceptionRecord(
    string Source,
    Exception Exception,
    bool? IsTerminating = null);

internal sealed record InitializationFailure(string Code, string UserMessage);

public sealed class CommandFailureException : Exception
{
    public CommandFailureException(string code, string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("A command failure code is required.", nameof(code))
            : code;
    }

    public string Code { get; }
}

internal interface IProcessExceptionHooks
{
    void SubscribeAppDomain(UnhandledExceptionEventHandler handler);
    void UnsubscribeAppDomain(UnhandledExceptionEventHandler handler);
    void SubscribeTaskScheduler(EventHandler<UnobservedTaskExceptionEventArgs> handler);
    void UnsubscribeTaskScheduler(EventHandler<UnobservedTaskExceptionEventArgs> handler);
}

internal sealed class ProcessExceptionHooks : IProcessExceptionHooks
{
    public static ProcessExceptionHooks Instance { get; } = new();

    private ProcessExceptionHooks() { }

    public void SubscribeAppDomain(UnhandledExceptionEventHandler handler) =>
        AppDomain.CurrentDomain.UnhandledException += handler;

    public void UnsubscribeAppDomain(UnhandledExceptionEventHandler handler) =>
        AppDomain.CurrentDomain.UnhandledException -= handler;

    public void SubscribeTaskScheduler(EventHandler<UnobservedTaskExceptionEventArgs> handler) =>
        TaskScheduler.UnobservedTaskException += handler;

    public void UnsubscribeTaskScheduler(EventHandler<UnobservedTaskExceptionEventArgs> handler) =>
        TaskScheduler.UnobservedTaskException -= handler;
}
