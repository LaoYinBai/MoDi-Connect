using MoDi.Desktop.Core.Session;
using MoDi.Desktop.Links;
using Xunit;

namespace MoDi.Desktop.Tests.Links;

public sealed class WifiLanLinkLifecycleTests
{
    [Fact]
    public async Task Three_reconnects_leave_one_public_callback_per_underlying_event()
    {
        using var fixture = new WifiLanFixture();

        for (var cycle = 0; cycle < 3; cycle++)
        {
            Assert.True(await fixture.Link.ConnectAsync());
            Assert.True(await fixture.Link.ConnectAsync());
            await fixture.Link.DisconnectAsync();
            Assert.Equal(LinkState.Idle, fixture.Link.State);
        }

        Assert.True(await fixture.Link.ConnectAsync());

        fixture.Handshake.RaiseHello(Guid.NewGuid());
        fixture.Engine.RaiseFirstFrame();
        fixture.Handshake.RaiseError("handshake failed");

        Assert.Equal(1, fixture.SessionStartedCount);
        Assert.Equal(1, fixture.StreamingStateCount);
        Assert.Equal(1, fixture.HandshakeErrorCount);
    }

    [Fact]
    public async Task Failed_connect_retries_leave_one_public_callback_per_underlying_event()
    {
        using var fixture = new WifiLanFixture();
        fixture.Mdns.FailStart = true;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.False(await fixture.Link.ConnectAsync());
            await fixture.Link.DisconnectAsync();
        }

        fixture.Mdns.FailStart = false;
        Assert.True(await fixture.Link.ConnectAsync());

        fixture.Handshake.RaiseHello(Guid.NewGuid());
        fixture.Engine.RaiseFirstFrame();
        fixture.Handshake.RaiseError("handshake failed");

        Assert.Equal(1, fixture.SessionStartedCount);
        Assert.Equal(1, fixture.StreamingStateCount);
        Assert.Equal(1, fixture.HandshakeErrorCount);
    }

    [Fact]
    public async Task Connect_and_disconnect_share_one_lifecycle_gate()
    {
        using var fixture = new WifiLanFixture();
        fixture.Mdns.BlockStart();

        var connect = fixture.Link.ConnectAsync();
        await fixture.Mdns.StartEntered;
        var disconnect = fixture.Link.DisconnectAsync();

        Assert.False(disconnect.IsCompleted);
        fixture.Mdns.ReleaseStart();

        Assert.True(await connect);
        await disconnect;
        Assert.Equal(LinkState.Idle, fixture.Link.State);
    }

    [Fact]
    public async Task Repeated_disconnect_after_dispose_does_not_touch_dependencies()
    {
        var fixture = new WifiLanFixture();
        Assert.True(await fixture.Link.ConnectAsync());

        fixture.Dispose();

        await fixture.Link.DisconnectAsync();
        await fixture.Link.DisconnectAsync();
        Assert.Equal(LinkState.Idle, fixture.Link.State);
    }

    [Fact]
    public async Task Dispose_removes_public_event_forwarding()
    {
        var fixture = new WifiLanFixture();
        Assert.True(await fixture.Link.ConnectAsync());

        fixture.Dispose();
        fixture.Handshake.RaiseHello(Guid.NewGuid());
        fixture.Engine.RaiseFirstFrame();
        fixture.Handshake.RaiseError("handshake failed");

        Assert.Equal(0, fixture.SessionStartedCount);
        Assert.Equal(0, fixture.StreamingStateCount);
        Assert.Equal(0, fixture.HandshakeErrorCount);
    }

    private sealed class WifiLanFixture : IDisposable
    {
        private bool _disposed;

        public WifiLanFixture()
        {
            Link = new WifiLanLink(StateManager, Engine, Handshake, Mdns);
            Link.OnSessionStarted += _ => SessionStartedCount++;
            Link.OnStateChanged += state =>
            {
                if (state == ConnectionState.Streaming)
                    StreamingStateCount++;
            };
            Link.OnStatusChanged += status =>
            {
                if (status == "handshake failed")
                    HandshakeErrorCount++;
            };
        }

        public ConnectionStateManager StateManager { get; } = new();
        public FakeAudioEngine Engine { get; } = new();
        public FakeHandshakeEndpoint Handshake { get; } = new();
        public FakeMdnsPublisher Mdns { get; } = new();
        public WifiLanLink Link { get; }
        public int SessionStartedCount { get; private set; }
        public int StreamingStateCount { get; private set; }
        public int HandshakeErrorCount { get; private set; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Link.Dispose();
        }
    }

    private sealed class FakeAudioEngine : IAudioEngine
    {
        private bool _disposed;

        public event Action? OnFirstFrameDecoded;
        public event Action? OnAudioTimeout;
        public event Action<bool>? OnMicOutputChanged;
        public event Action<string>? OnError;

        public float Volume { get; set; } = 1;

        public void Start() => ThrowIfDisposed();
        public void Stop() => ThrowIfDisposed();
        public void ResetSession() => ThrowIfDisposed();
        public bool SetMode(AudioRouter.RouteMode mode)
        {
            ThrowIfDisposed();
            return true;
        }

        public void RaiseFirstFrame() => OnFirstFrameDecoded?.Invoke();
        public void RaiseTimeout() => OnAudioTimeout?.Invoke();
        public void RaiseMicOutputChanged(bool toMic) => OnMicOutputChanged?.Invoke(toMic);
        public void RaiseError(string message) => OnError?.Invoke(message);

        public void Dispose() => _disposed = true;

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class FakeHandshakeEndpoint : IHandshakeEndpoint
    {
        private bool _disposed;

        public event Action<HelloSessionIdentity>? OnHelloReceived;
        public event Action<string>? OnError;

        public Func<SessionControlMessage, byte, SessionControlMessage>? OnDisconnectRequest { get; set; }

        public void Start() => ThrowIfDisposed();
        public void Stop() => ThrowIfDisposed();

        public void RaiseHello(Guid sessionId) =>
            OnHelloReceived?.Invoke(new HelloSessionIdentity(0, null, sessionId));

        public void RaiseError(string message) => OnError?.Invoke(message);

        public void Dispose() => _disposed = true;

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class FakeMdnsPublisher : IMdnsPublisher
    {
        private TaskCompletionSource? _startEntered;
        private TaskCompletionSource? _startRelease;
        private bool _disposed;

        public bool FailStart { get; set; }
        public Task StartEntered => _startEntered?.Task ?? Task.CompletedTask;

        public void BlockStart()
        {
            _startEntered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _startRelease = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void ReleaseStart() => _startRelease?.TrySetResult();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            _startEntered?.TrySetResult();
            if (_startRelease is not null)
                await _startRelease.Task.WaitAsync(cancellationToken);
            if (FailStart)
                throw new InvalidOperationException("start failed");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return Task.CompletedTask;
        }

        public void Dispose() => _disposed = true;

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
