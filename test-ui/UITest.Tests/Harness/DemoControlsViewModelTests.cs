using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UITest.Demo;
using UITest.Fakes;
using Xunit;

namespace UITest.Tests.Harness;

public sealed class DemoControlsViewModelTests
{
    [Fact]
    public void Automatic_rms_pulse_is_marshaled_to_the_captured_ui_context()
    {
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var timeProvider = new ManualTimeProvider();
            var receiver = new FakeReceiverStatusSource();
            using var viewModel = new DemoControlsViewModel(
                receiver,
                new FakeAppearanceService(),
                new FakePairingService(timeProvider),
                new FakePluginCatalogService(),
                timeProvider);
            viewModel.AutoRms = true;
            var originalRms = receiver.Snapshot.Rms;

            timeProvider.Timer.Fire();

            Assert.Equal(1, context.PostCalls);
            Assert.Equal(originalRms, receiver.Snapshot.Rms);

            context.RunPostedCallbacks();
            Assert.NotEqual(originalRms, receiver.Snapshot.Rms);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public int PostCalls { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostCalls++;
            _callbacks.Enqueue((d, state));
        }

        public void RunPostedCallbacks()
        {
            while (_callbacks.TryDequeue(out var item))
                item.Callback(item.State);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public ManualTimer Timer { get; private set; } = null!;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(123_456);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Timer = new ManualTimer(callback, state);
            return Timer;
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Fire() => callback(state);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
