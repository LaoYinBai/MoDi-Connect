using MoDi.Desktop.Network;
using Xunit;

namespace MoDi.Desktop.Tests.Network;

public sealed class MdnsPublisherTests
{
    [Fact(Timeout = 10_000)]
    public async Task Address_change_burst_restarts_once()
    {
        using var fixture = new MdnsFixture();
        await fixture.Publisher.StartAsync(CancellationToken.None);

        fixture.Network.RaiseChanged(3);
        await fixture.Time.AdvanceAsync(TimeSpan.FromMilliseconds(999));
        Assert.Equal(1, fixture.Advertiser.StartCount);

        var restarted = fixture.Advertiser.WaitForStartCountAsync(2);
        await fixture.Time.AdvanceAsync(TimeSpan.FromMilliseconds(1));
        await restarted;

        Assert.Equal(2, fixture.Advertiser.StartCount);
        Assert.Equal(1, fixture.Advertiser.StopCount);
    }

    [Fact(Timeout = 10_000)]
    public async Task Persistent_failure_makes_five_attempts_on_exponential_schedule()
    {
        using var fixture = new MdnsFixture(alwaysFailStart: true);

        var start = fixture.Publisher.StartAsync(CancellationToken.None);
        Assert.Equal(1, fixture.Advertiser.StartCount);

        foreach (var expected in new[]
                 {
                     (Delay: 1, Attempts: 2),
                     (Delay: 2, Attempts: 3),
                     (Delay: 4, Attempts: 4),
                     (Delay: 8, Attempts: 5),
                 })
        {
            var attempted = fixture.Advertiser.WaitForStartCountAsync(expected.Attempts);
            await fixture.Time.AdvanceAsync(TimeSpan.FromSeconds(expected.Delay));
            await attempted;
            Assert.Equal(expected.Attempts, fixture.Advertiser.StartCount);
        }

        await fixture.Time.AdvanceAsync(TimeSpan.FromSeconds(16));
        await start;

        Assert.Equal(5, fixture.Advertiser.StartCount);
        Assert.False(fixture.Publisher.IsRunning);
    }

    [Fact(Timeout = 10_000)]
    public async Task Advertise_failure_does_not_report_running_before_retry_succeeds()
    {
        using var fixture = new MdnsFixture(advertiseFailures: 1);

        var start = fixture.Publisher.StartAsync(CancellationToken.None);

        Assert.False(fixture.Publisher.IsRunning);
        var retried = fixture.Advertiser.WaitForStartCountAsync(2);
        await fixture.Time.AdvanceAsync(TimeSpan.FromSeconds(1));
        await retried;
        await start;

        Assert.True(fixture.Publisher.IsRunning);
        Assert.Equal(2, fixture.Advertiser.StartCount);
        Assert.Equal(2, fixture.Advertiser.AdvertiseCount);
    }

    [Fact]
    public async Task Dispose_after_network_change_prevents_restart()
    {
        var fixture = new MdnsFixture();
        await fixture.Publisher.StartAsync(CancellationToken.None);
        fixture.Network.RaiseChanged();

        fixture.Dispose();
        await fixture.Time.AdvanceAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(1, fixture.Advertiser.StartCount);
        Assert.Equal(0, fixture.Network.SubscriberCount);
    }

    private sealed class MdnsFixture : IDisposable
    {
        public MdnsFixture(bool alwaysFailStart = false, int advertiseFailures = 0)
        {
            Advertiser = new FakeAdvertiser(alwaysFailStart, advertiseFailures);
            Publisher = new MdnsPublisher(Advertiser, Network, Time);
        }

        public FakeNetworkChangeSource Network { get; } = new();
        public FakeAdvertiser Advertiser { get; }
        public ManualTimeProvider Time { get; } = new();
        public MdnsPublisher Publisher { get; }

        public void Dispose() => Publisher.Dispose();
    }

    private sealed class FakeNetworkChangeSource : INetworkChangeSource
    {
        private EventHandler? _changed;

        public event EventHandler? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;

        public void RaiseChanged(int count = 1)
        {
            for (var i = 0; i < count; i++)
                _changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => _changed = null;
    }

    private sealed class FakeAdvertiser(bool alwaysFailStart, int advertiseFailures) : IMdnsAdvertiser
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, TaskCompletionSource> _startWaiters = [];
        private int _startCount;
        private int _advertiseCount;
        private int _stopCount;

        public int StartCount => Volatile.Read(ref _startCount);
        public int AdvertiseCount => Volatile.Read(ref _advertiseCount);
        public int StopCount => Volatile.Read(ref _stopCount);

        public Task WaitForStartCountAsync(int expected)
        {
            lock (_gate)
            {
                if (_startCount >= expected)
                    return Task.CompletedTask;

                if (!_startWaiters.TryGetValue(expected, out var waiter))
                {
                    waiter = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _startWaiters.Add(expected, waiter);
                }
                return waiter.Task;
            }
        }

        public void Start()
        {
            TaskCompletionSource? waiter;
            int count;
            lock (_gate)
            {
                count = ++_startCount;
                _startWaiters.Remove(count, out waiter);
            }
            waiter?.TrySetResult();
            if (alwaysFailStart)
                throw new InvalidOperationException("start failed");
        }

        public void Advertise()
        {
            var count = Interlocked.Increment(ref _advertiseCount);
            if (count <= advertiseFailures)
                throw new InvalidOperationException("advertise failed");
        }

        public void Stop() => Interlocked.Increment(ref _stopCount);
        public void Dispose() { }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _utcNow = new(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
                return _utcNow;
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            lock (_gate)
                _timers.Add(timer);
            return timer;
        }

        public async Task AdvanceAsync(TimeSpan delta)
        {
            List<ManualTimer> due;
            lock (_gate)
            {
                _utcNow += delta;
                due = _timers.Where(timer => timer.IsDue(_utcNow)).ToList();
            }

            foreach (var timer in due)
                timer.Fire();

            for (var i = 0; i < 10; i++)
                await Task.Yield();
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider _owner;
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private DateTimeOffset _dueAt;
            private TimeSpan _period;
            private bool _disposed;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _owner = owner;
                _callback = callback;
                _state = state;
                _dueAt = owner.GetUtcNow() + dueTime;
                _period = period;
            }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (_owner._gate)
                {
                    if (_disposed)
                        return false;
                    _dueAt = _owner._utcNow + dueTime;
                    _period = period;
                    return true;
                }
            }

            public bool IsDue(DateTimeOffset now) => !_disposed && _dueAt <= now;

            public void Fire()
            {
                lock (_owner._gate)
                {
                    if (_disposed)
                        return;
                    if (_period == Timeout.InfiniteTimeSpan)
                        _dueAt = DateTimeOffset.MaxValue;
                    else
                        _dueAt += _period;
                }
                _callback(_state);
            }

            public void Dispose()
            {
                lock (_owner._gate)
                {
                    _disposed = true;
                    _owner._timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
