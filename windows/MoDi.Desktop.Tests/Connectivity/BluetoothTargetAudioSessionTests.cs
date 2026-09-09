using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts.Connectivity;
using MoDi.Desktop.Connectivity.Bluetooth;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Connectivity;

public sealed class BluetoothTargetAudioSessionTests
{
    [Fact]
    public async Task Authenticated_session_routes_unchanged_bytes_without_owning_physical_connection()
    {
        var legacy = new FakeTransport();
        var target = new BluetoothTargetAudioSession(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            legacy);

        target.Bind();
        await target.AudioTransport.ConnectAsync();
        ReadOnlyMemory<byte> received = default;
        target.AudioTransport.PacketReceived += bytes => received = bytes;
        await target.AudioTransport.SendAsync(new byte[] { 1, 2, 3 });
        legacy.Emit(new byte[] { 4, 5, 6 });
        target.ObserveStreaming();
        target.Close();

        Assert.Equal(TransportKind.Bluetooth, target.Session.Transport);
        Assert.Equal(SessionState.Closed, target.Session.State);
        Assert.Equal(new byte[] { 1, 2, 3 }, legacy.LastSent.ToArray());
        Assert.Equal(new byte[] { 4, 5, 6 }, received.ToArray());
        Assert.Equal(0, legacy.DisconnectCalls);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void Feature_gate_is_explicit_and_defaults_off(string? value, bool expected) =>
        Assert.Equal(expected, BluetoothTargetComposition.IsEnabled(_ => value));

    private sealed class FakeTransport : ITransport
    {
        public event Action<ReadOnlyMemory<byte>>? PacketReceived;
        public bool IsConnected => true;
        public TransportType Type => TransportType.Bluetooth;
        public ReadOnlyMemory<byte> LastSent { get; private set; }
        public int DisconnectCalls { get; private set; }
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) { LastSent = data.ToArray(); return Task.CompletedTask; }
        public Task DisconnectAsync() { DisconnectCalls++; return Task.CompletedTask; }
        public void Emit(ReadOnlyMemory<byte> data) => PacketReceived?.Invoke(data);
    }
}
