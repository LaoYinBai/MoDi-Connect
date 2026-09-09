using System;
using MoDi.App.Contracts.Connectivity;
using MoDi.Desktop.Connectivity.Legacy;
using MoDi.Protocol;

namespace MoDi.Desktop.Connectivity.Bluetooth;

internal sealed class BluetoothTargetAudioSession
{
    private readonly AuthenticatedLegacyAudioSession _inner;

    internal BluetoothTargetAudioSession(Guid sessionUuid, ITransport legacy) =>
        _inner = new AuthenticatedLegacyAudioSession(
            sessionUuid,
            PeerId.Parse($"bluetooth-session:{sessionUuid:N}"),
            TransportKind.Bluetooth,
            TransportType.Bluetooth,
            legacy);

    internal SessionRegistry Sessions => _inner.Sessions;
    internal ChannelRouter Channels => _inner.Channels;
    internal Peer Peer => _inner.Peer;
    internal Session Session => _inner.Session;
    internal ITransport AudioTransport => _inner.AudioTransport;
    internal void Bind() => _inner.Bind();
    internal void ObserveStreaming() => _inner.ObserveStreaming();
    internal void Close() => _inner.Close();
}

internal static class BluetoothTargetComposition
{
    internal static bool IsEnabled(Func<string, string?>? readSetting = null)
    {
        readSetting ??= Environment.GetEnvironmentVariable;
        return string.Equals(readSetting("MODI_BLUETOOTH_TARGET"), "1", StringComparison.Ordinal);
    }
}
