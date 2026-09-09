using MoDi.App.Contracts.Connectivity;
using MoDi.Core;
using MoDi.Protocol;
using MoDi.Desktop.Connectivity.Transports;
using Xunit;

namespace MoDi.Desktop.Tests.Connectivity;

public sealed class LegacyTransportSessionAdapterTests
{
    [Fact]
    public async Task Adapter_preserves_bytes_and_owns_connect_send_close()
    {
        var legacy = new FakeTransport();
        await using var session = new LegacyTransportSessionAdapter(
            TransportDescriptor.For(TransportKind.Lan), legacy);
        ReadOnlyMemory<byte> received = default;
        session.BytesReceived += bytes => received = bytes;

        await session.ConnectAsync(TransportEndpoint.Parse("opaque-lan-endpoint"), CancellationToken.None);
        await session.SendAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        legacy.Emit(new byte[] { 4, 5, 6 });
        await session.CloseAsync(CancellationToken.None);

        Assert.Equal(1, legacy.ConnectCalls);
        Assert.Equal(new byte[] { 1, 2, 3 }, legacy.LastSent.ToArray());
        Assert.Equal(new byte[] { 4, 5, 6 }, received.ToArray());
        Assert.Equal(1, legacy.DisconnectCalls);
        Assert.Equal(TransportSessionState.Closed, session.State);
    }

    private sealed class FakeTransport : ITransport
    {
        public event Action<ReadOnlyMemory<byte>>? PacketReceived;
        public TransportType Type => TransportType.Udp;
        public bool IsConnected { get; private set; }
        public int ConnectCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public ReadOnlyMemory<byte> LastSent { get; private set; }
        public Task ConnectAsync(CancellationToken ct = default) { ConnectCalls++; IsConnected = true; return Task.CompletedTask; }
        public Task DisconnectAsync() { DisconnectCalls++; IsConnected = false; return Task.CompletedTask; }
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { LastSent = data.ToArray(); return Task.CompletedTask; }
        public void Emit(ReadOnlyMemory<byte> data) => PacketReceived?.Invoke(data);
    }
}
