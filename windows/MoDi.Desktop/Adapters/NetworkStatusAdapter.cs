using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MoDi.App.Contracts;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Adapters;

internal interface ILocalAddressResolver
{
    string? GetPreferredIpv4();
}

internal sealed class LocalAddressResolver : ILocalAddressResolver
{
    public string? GetPreferredIpv4() => Dns.GetHostAddresses(Dns.GetHostName())
        .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork
            && !IPAddress.IsLoopback(address))
        ?.ToString();
}

public sealed class NetworkStatusAdapter : INetworkStatusSource
{
    private const int AudioPort = 12345;
    private const int HandshakePort = 12347;
    private readonly IReceiverRuntime _runtime;
    private readonly ILocalAddressResolver _addressResolver;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    public NetworkStatusAdapter(ReceiverController controller)
        : this(new ReceiverRuntime(controller), new LocalAddressResolver()) { }

    internal NetworkStatusAdapter(IReceiverRuntime runtime, ILocalAddressResolver addressResolver)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
        _uiContext = SynchronizationContext.Current;
        Snapshot = BuildSnapshot();
        _runtime.SnapshotChanged += OnRuntimeChanged;
    }

    public NetworkStatusSnapshot Snapshot { get; private set; }
    public event Action<NetworkStatusSnapshot>? SnapshotChanged;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _runtime.SnapshotChanged -= OnRuntimeChanged;
    }

    private void OnRuntimeChanged()
    {
        if (_disposed)
            return;
        Snapshot = BuildSnapshot();
        var publishedSnapshot = Snapshot;
        var handler = SnapshotChanged;
        if (handler is null)
            return;
        if (_uiContext is null || ReferenceEquals(SynchronizationContext.Current, _uiContext))
            handler(publishedSnapshot);
        else
            _uiContext.Post(_ => handler(publishedSnapshot), null);
    }

    private NetworkStatusSnapshot BuildSnapshot()
    {
        var activeLink = ReceiverStatusAdapter.MapLink(_runtime.ActiveLink, out _);
        return new NetworkStatusSnapshot(
            LinkLabel(activeLink),
            ResolveAddress(),
            AudioPort,
            HandshakePort,
            ReceiverStatusAdapter.BuildLinks(_runtime, activeLink));
    }

    private string ResolveAddress()
    {
        try
        {
            return _addressResolver.GetPreferredIpv4() is { Length: > 0 } address ? address : "不可用";
        }
        catch
        {
            return "不可用";
        }
    }

    private static string LinkLabel(LinkKind link) => link switch
    {
        LinkKind.None => "当前无活跃链路",
        LinkKind.WifiDirect => "万能 · WiFi Direct",
        LinkKind.Bluetooth => "蓝牙 · RFCOMM",
        LinkKind.Usb => "USB · ADB",
        _ => "在家 · WiFi LAN",
    };
}
