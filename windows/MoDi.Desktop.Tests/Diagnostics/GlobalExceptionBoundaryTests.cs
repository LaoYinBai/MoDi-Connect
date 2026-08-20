using MoDi.Desktop.Diagnostics;
using Xunit;

namespace MoDi.Desktop.Tests.Diagnostics;

public sealed class GlobalExceptionBoundaryTests
{
    [Fact]
    public void Registration_and_disposal_are_idempotent()
    {
        var hooks = new RecordingProcessExceptionHooks();
        using var boundary = GlobalExceptionBoundary.CreateForTest(hooks, _ => { });

        boundary.InstallForTest();
        boundary.InstallForTest();

        Assert.Equal(1, hooks.AppDomainSubscriptions);
        Assert.Equal(1, hooks.TaskSchedulerSubscriptions);

        boundary.Dispose();
        boundary.Dispose();

        Assert.Equal(1, hooks.AppDomainUnsubscriptions);
        Assert.Equal(1, hooks.TaskSchedulerUnsubscriptions);
    }

    [Fact]
    public void AppDomain_failure_records_termination_state_and_exception()
    {
        var hooks = new RecordingProcessExceptionHooks();
        var sink = new RecordingFatalSink();
        using var boundary = GlobalExceptionBoundary.CreateForTest(hooks, sink.Record);
        boundary.InstallForTest();
        var exception = new IOException("boom");

        hooks.RaiseAppDomain(exception, isTerminating: true);

        var recorded = Assert.Single(sink.Events);
        Assert.Equal("AppDomain.UnhandledException", recorded.Source);
        Assert.True(recorded.IsTerminating);
        Assert.Same(exception, recorded.Exception);
    }

    [Fact]
    public void Unobserved_task_failure_is_flattened_logged_and_marked_observed()
    {
        var hooks = new RecordingProcessExceptionHooks();
        var sink = new RecordingFatalSink();
        using var boundary = GlobalExceptionBoundary.CreateForTest(hooks, sink.Record);
        boundary.InstallForTest();
        var first = new IOException("disk");
        var second = new InvalidOperationException("state");
        var eventArgs = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new AggregateException(first), second));

        hooks.RaiseTaskScheduler(eventArgs);

        Assert.True(eventArgs.Observed);
        Assert.Equal(2, sink.Events.Count);
        Assert.All(
            sink.Events,
            item => Assert.Equal("TaskScheduler.UnobservedTaskException", item.Source));
        Assert.Contains(sink.Events, item => ReferenceEquals(first, item.Exception));
        Assert.Contains(sink.Events, item => ReferenceEquals(second, item.Exception));
    }

    [Fact]
    public void Observed_task_fault_is_logged_with_operation_name()
    {
        var sink = new RecordingFatalSink();

        GlobalExceptionBoundary.ObserveForTest(
            Task.FromException(new IOException("boom")),
            "AudioEngine.TransportConnect",
            sink.Record);

        var recorded = Assert.Single(sink.Events);
        Assert.Equal("AudioEngine.TransportConnect", recorded.Source);
        Assert.IsType<IOException>(recorded.Exception);
    }

    [Fact]
    public void Observed_owned_cancellation_is_not_reported_as_failure()
    {
        var sink = new RecordingFatalSink();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        GlobalExceptionBoundary.ObserveForTest(
            Task.FromCanceled(cancellation.Token),
            "WifiDirect.SendHello",
            sink.Record);

        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Dispatcher_only_handles_explicit_command_failures()
    {
        var sink = new RecordingFatalSink();

        var commandHandled = GlobalExceptionBoundary.HandleDispatcherExceptionForTest(
            new CommandFailureException("SAVE_FAILED", "Could not save"),
            sink.Record);
        var unknownHandled = GlobalExceptionBoundary.HandleDispatcherExceptionForTest(
            new IOException("boom"),
            sink.Record);

        Assert.True(commandHandled);
        Assert.False(unknownHandled);
        Assert.Collection(
            sink.Events,
            item => Assert.Equal("Avalonia.CommandFailure", item.Source),
            item => Assert.Equal("Avalonia.DispatcherUnhandledException", item.Source));
    }

    [Fact]
    public void Initialization_failure_has_stable_code_and_is_recorded()
    {
        var sink = new RecordingFatalSink();
        var exception = new IOException("assets unavailable");

        var failure = GlobalExceptionBoundary.ReportInitializationFailureForTest(
            exception,
            sink.Record);

        Assert.Equal("APP_INITIALIZE_FAILED", failure.Code);
        Assert.Contains("初始化失败", failure.UserMessage);
        var recorded = Assert.Single(sink.Events);
        Assert.Equal("APP_INITIALIZE_FAILED", recorded.Source);
        Assert.Same(exception, recorded.Exception);
    }

    private sealed class RecordingFatalSink
    {
        public List<GlobalExceptionRecord> Events { get; } = [];

        public void Record(GlobalExceptionRecord item) => Events.Add(item);
    }

    private sealed class RecordingProcessExceptionHooks : IProcessExceptionHooks
    {
        private UnhandledExceptionEventHandler? _appDomainHandler;
        private EventHandler<UnobservedTaskExceptionEventArgs>? _taskSchedulerHandler;

        public int AppDomainSubscriptions { get; private set; }
        public int AppDomainUnsubscriptions { get; private set; }
        public int TaskSchedulerSubscriptions { get; private set; }
        public int TaskSchedulerUnsubscriptions { get; private set; }

        public void SubscribeAppDomain(UnhandledExceptionEventHandler handler)
        {
            AppDomainSubscriptions++;
            _appDomainHandler += handler;
        }

        public void UnsubscribeAppDomain(UnhandledExceptionEventHandler handler)
        {
            AppDomainUnsubscriptions++;
            _appDomainHandler -= handler;
        }

        public void SubscribeTaskScheduler(EventHandler<UnobservedTaskExceptionEventArgs> handler)
        {
            TaskSchedulerSubscriptions++;
            _taskSchedulerHandler += handler;
        }

        public void UnsubscribeTaskScheduler(EventHandler<UnobservedTaskExceptionEventArgs> handler)
        {
            TaskSchedulerUnsubscriptions++;
            _taskSchedulerHandler -= handler;
        }

        public void RaiseAppDomain(Exception exception, bool isTerminating) =>
            _appDomainHandler?.Invoke(this, new UnhandledExceptionEventArgs(exception, isTerminating));

        public void RaiseTaskScheduler(UnobservedTaskExceptionEventArgs eventArgs) =>
            _taskSchedulerHandler?.Invoke(this, eventArgs);
    }
}
