using MoDi.Desktop;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Lifecycle;

public sealed class HandshakeEndpointLifecycleTests
{
    [Fact]
    public async Task Start_and_stop_wait_for_owned_transport_operations()
    {
        var transport = new BlockingTransport();
        using var endpoint = new HandshakeEndpoint(transport);

        var start = endpoint.StartAsync(CancellationToken.None);
        await transport.ConnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(start.IsCompleted);
        transport.ReleaseConnect.TrySetResult();
        await start;

        var stop = endpoint.StopAsync(CancellationToken.None);
        await transport.DisconnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(stop.IsCompleted);
        transport.ReleaseDisconnect.TrySetResult();
        await stop;
    }

    [Fact]
    public async Task Repeated_start_and_stop_do_not_duplicate_transport_lifecycle()
    {
        var transport = BlockingTransport.Immediate();
        using var endpoint = new HandshakeEndpoint(transport);

        await endpoint.StartAsync(CancellationToken.None);
        await endpoint.StartAsync(CancellationToken.None);
        await endpoint.StopAsync(CancellationToken.None);
        await endpoint.StopAsync(CancellationToken.None);

        Assert.Equal(1, transport.ConnectCalls);
        Assert.Equal(1, transport.DisconnectCalls);
    }

    private sealed class BlockingTransport : ITransport
    {
        public event Action<ReadOnlyMemory<byte>>? PacketReceived { add { } remove { } }
        public TransportType Type => TransportType.Udp;
        public bool IsConnected { get; private set; }
        public TaskCompletionSource ConnectEntered { get; } = NewSignal();
        public TaskCompletionSource DisconnectEntered { get; } = NewSignal();
        public TaskCompletionSource ReleaseConnect { get; private init; } = NewSignal();
        public TaskCompletionSource ReleaseDisconnect { get; private init; } = NewSignal();
        public int ConnectCalls { get; private set; }
        public int DisconnectCalls { get; private set; }

        public static BlockingTransport Immediate()
        {
            var transport = new BlockingTransport();
            transport.ReleaseConnect.TrySetResult();
            transport.ReleaseDisconnect.TrySetResult();
            return transport;
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCalls++;
            ConnectEntered.TrySetResult();
            await ReleaseConnect.Task.WaitAsync(ct);
            IsConnected = true;
        }

        public async Task DisconnectAsync()
        {
            DisconnectCalls++;
            DisconnectEntered.TrySetResult();
            await ReleaseDisconnect.Task;
            IsConnected = false;
        }

        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => Task.CompletedTask;

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
