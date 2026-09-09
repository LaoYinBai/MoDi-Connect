using System;
using MoDi.App.Contracts.Connectivity;
using MoDi.Desktop.Connectivity.Legacy;
using MoDi.Protocol;

namespace MoDi.Desktop.Connectivity.Usb;

internal sealed class UsbTargetAudioSession
{
    private readonly AuthenticatedLegacyAudioSession _inner;

    internal UsbTargetAudioSession(Guid sessionUuid, ITransport legacy) =>
        _inner = new AuthenticatedLegacyAudioSession(
            sessionUuid,
            PeerId.Parse($"usb-session:{sessionUuid:N}"),
            TransportKind.Usb,
            TransportType.Usb,
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

internal static class UsbTargetComposition
{
    internal static bool IsEnabled(Func<string, string?>? readSetting = null)
    {
        readSetting ??= Environment.GetEnvironmentVariable;
        return string.Equals(readSetting("MODI_USB_TARGET"), "1", StringComparison.Ordinal);
    }
}
