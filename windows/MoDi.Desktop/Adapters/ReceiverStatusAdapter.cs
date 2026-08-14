using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Desktop.Links;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Adapters;

public sealed class ReceiverStatusAdapter : IReceiverStatusSource
{
    private readonly IReceiverRuntime _runtime;
    private readonly SynchronizationContext? _uiContext;
    private readonly object _initializeLock = new();
    private Task<OperationResult>? _initialization;
    private string? _adapterErrorCode;
    private string? _adapterErrorMessage;
    private bool _disposed;

    public ReceiverStatusAdapter(ReceiverController controller)
        : this(new ReceiverRuntime(controller)) { }

    internal ReceiverStatusAdapter(IReceiverRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _uiContext = SynchronizationContext.Current;
        Snapshot = BuildSnapshot();
        _runtime.SnapshotChanged += OnRuntimeChanged;
    }

    public ReceiverSnapshot Snapshot { get; private set; }
    public event Action<ReceiverSnapshot>? SnapshotChanged;

    public Task<OperationResult> InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_initializeLock)
            return _initialization ??= InitializeCoreAsync();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _runtime.SnapshotChanged -= OnRuntimeChanged;
    }

    internal static LinkKind MapLink(string? link, out bool isKnown)
    {
        switch (link?.Trim().ToLowerInvariant())
        {
            case "none":
                isKnown = true;
                return LinkKind.None;
            case "lan":
                isKnown = true;
                return LinkKind.Lan;
            case "wifi-direct":
                isKnown = true;
                return LinkKind.WifiDirect;
            case "bluetooth":
                isKnown = true;
                return LinkKind.Bluetooth;
            case "usb":
                isKnown = true;
                return LinkKind.Usb;
            default:
                isKnown = false;
                return LinkKind.None;
        }
    }

    internal static string OutputLabel(int route) => route >= 2
        ? "CABLE Input（虚拟麦克风）"
        : "系统默认扬声器";

    internal static IReadOnlyList<LinkStatusSnapshot> BuildLinks(IReceiverRuntime runtime, LinkKind activeLink)
    {
        var receiverState = MapState(runtime.ConnectionState);
        return
        [
            BuildLink(LinkKind.Lan, "在家 · WiFi LAN", runtime.LanStatus),
            BuildLink(LinkKind.WifiDirect, "万能 · WiFi Direct", runtime.P2pStatus),
            BuildLink(LinkKind.Bluetooth, "蓝牙 · RFCOMM", runtime.BluetoothStatus),
            BuildLink(LinkKind.Usb, "USB · ADB", runtime.UsbStatus),
        ];

        LinkStatusSnapshot BuildLink(LinkKind kind, string label, string detail) => new(
            kind,
            MapAvailability(kind, detail, activeLink, receiverState, runtime.IsP2pProgressVisible),
            label,
            detail ?? string.Empty);
    }

    internal static LinkAvailability MapAvailability(
        LinkKind kind,
        string? status,
        LinkKind activeLink,
        ReceiverState receiverState,
        bool isP2pProgressVisible)
    {
        if (kind == activeLink && receiverState is ReceiverState.Connected or ReceiverState.Streaming)
            return LinkAvailability.Active;
        if (kind == LinkKind.WifiDirect && isP2pProgressVisible)
            return LinkAvailability.Connecting;

        var text = status?.Trim() ?? string.Empty;
        if (text.Contains("失败", StringComparison.OrdinalIgnoreCase)
            || text.Contains("错误", StringComparison.OrdinalIgnoreCase)
            || text.Contains("异常", StringComparison.OrdinalIgnoreCase))
            return LinkAvailability.Error;
        if (text.Contains("启动", StringComparison.OrdinalIgnoreCase) && text != "等待启动")
            return LinkAvailability.Starting;
        return text == "等待启动" ? LinkAvailability.Inactive : LinkAvailability.Listening;
    }

    private async Task<OperationResult> InitializeCoreAsync()
    {
        try
        {
            await _runtime.InitializeAsync();
            _adapterErrorCode = null;
            _adapterErrorMessage = null;
            Publish();
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _adapterErrorCode = "RECEIVER_INITIALIZE";
            _adapterErrorMessage = $"接收服务初始化失败：{ex.Message}";
            Publish();
            return OperationResult.Failure(_adapterErrorCode, _adapterErrorMessage);
        }
    }

    private void OnRuntimeChanged() => Publish();

    private void Publish()
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

    private ReceiverSnapshot BuildSnapshot()
    {
        var activeLink = MapLink(_runtime.ActiveLink, out var knownLink);
        var errorCode = knownLink ? _adapterErrorCode : "RECEIVER_UNKNOWN_LINK";
        var errorMessage = knownLink
            ? _adapterErrorMessage ?? NullIfBlank(_runtime.LastError)
            : $"无法识别接收链路：{_runtime.ActiveLink}";
        if (errorCode is null && errorMessage is not null)
            errorCode = "RECEIVER_RUNTIME";

        return new ReceiverSnapshot(
            _adapterErrorCode is null ? MapState(_runtime.ConnectionState) : ReceiverState.Error,
            string.IsNullOrWhiteSpace(_runtime.StatusMessage) ? "就绪：等待手机连接" : _runtime.StatusMessage,
            activeLink,
            _runtime.CurrentRoute,
            WifiLanLink.ModeLabel(_runtime.CurrentRoute),
            OutputLabel(_runtime.CurrentRoute),
            0,
            BuildLinks(_runtime, activeLink),
            _runtime.IsP2pProgressVisible,
            _runtime.IsP2pProgressIndeterminate,
            double.IsFinite(_runtime.P2pProgress) ? Math.Clamp(_runtime.P2pProgress, 0, 1) : 0,
            errorCode,
            errorMessage);
    }

    private static ReceiverState MapState(ConnectionState state) => state switch
    {
        ConnectionState.Searching => ReceiverState.Searching,
        ConnectionState.Found => ReceiverState.Found,
        ConnectionState.Connecting => ReceiverState.Connecting,
        ConnectionState.Connected => ReceiverState.Connected,
        ConnectionState.Streaming => ReceiverState.Streaming,
        ConnectionState.Reconnecting => ReceiverState.Reconnecting,
        ConnectionState.Error => ReceiverState.Error,
        _ => ReceiverState.Idle,
    };

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
