using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Adapters;

public sealed class PairingAdapter : IPairingService
{
    private const string RecentDeviceId = "recent-p2p";
    private readonly IReceiverRuntime _runtime;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, byte[]> _qrGenerator;
    private readonly SynchronizationContext? _uiContext;
    private ReadOnlyMemory<byte> _qrPng;
    private string _deviceName = "本机";
    private DateTimeOffset? _expiresAt;
    private bool _isRefreshing;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public PairingAdapter(ReceiverController controller, TimeProvider timeProvider)
        : this(new ReceiverRuntime(controller), timeProvider, payload => QrCodeHelper.GeneratePng(payload)) { }

    internal PairingAdapter(
        IReceiverRuntime runtime,
        TimeProvider timeProvider,
        Func<string, byte[]> qrGenerator)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _qrGenerator = qrGenerator ?? throw new ArgumentNullException(nameof(qrGenerator));
        _uiContext = SynchronizationContext.Current;
        Snapshot = BuildSnapshot();
        _runtime.SnapshotChanged += OnRuntimeChanged;
        _runtime.QrPayloadChanged += OnQrPayloadChanged;
    }

    public PairingSnapshot Snapshot { get; private set; }
    public event Action<PairingSnapshot>? SnapshotChanged;

    public async Task<OperationResult> RefreshQrAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isRefreshing = true;
        _errorCode = null;
        _errorMessage = null;
        Publish();
        try
        {
            await _runtime.RefreshP2pAsync();
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _errorCode = "PAIR_REFRESH";
            _errorMessage = $"刷新二维码失败：{ex.Message}";
            return OperationResult.Failure(_errorCode, _errorMessage);
        }
        finally
        {
            _isRefreshing = false;
            Publish();
        }
    }

    public async Task<OperationResult> ConnectAsync(string deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (deviceId != RecentDeviceId || _runtime.GetRecentPair() is null)
            return OperationResult.Failure("PAIR_DEVICE_NOT_FOUND", "找不到可重新连接的配对设备");

        try
        {
            await _runtime.ConnectRecentP2pAsync();
            _errorCode = null;
            _errorMessage = null;
            Publish();
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _errorCode = "PAIR_CONNECT";
            _errorMessage = $"重新连接失败：{ex.Message}";
            Publish();
            return OperationResult.Failure(_errorCode, _errorMessage);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _runtime.SnapshotChanged -= OnRuntimeChanged;
        _runtime.QrPayloadChanged -= OnQrPayloadChanged;
    }

    private void OnRuntimeChanged() => Publish();

    private void OnQrPayloadChanged(string? payload, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(deviceName))
        {
            _qrPng = ReadOnlyMemory<byte>.Empty;
            _expiresAt = null;
            Publish();
            return;
        }

        try
        {
            _qrPng = _qrGenerator(payload);
            _deviceName = deviceName;
            _expiresAt = _timeProvider.GetUtcNow().AddMinutes(2);
            _errorCode = null;
            _errorMessage = null;
        }
        catch (Exception ex)
        {
            _qrPng = ReadOnlyMemory<byte>.Empty;
            _expiresAt = null;
            _errorCode = "PAIR_QR_GENERATION";
            _errorMessage = $"生成配对二维码失败：{ex.Message}";
        }
        Publish();
    }

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

    private PairingSnapshot BuildSnapshot() => new(
        _qrPng,
        _deviceName,
        _expiresAt,
        BuildDevices(),
        _isRefreshing,
        _errorCode,
        _errorMessage);

    private IReadOnlyList<PairedDeviceSnapshot> BuildDevices()
    {
        var pair = _runtime.GetRecentPair();
        if (pair is null)
            return [];
        var label = pair.LastConnected == DateTime.MinValue
            ? "尚未完成首次连接"
            : $"上次连接：{pair.LastConnected:yyyy-MM-dd HH:mm}";
        return
        [
            new PairedDeviceSnapshot(
                RecentDeviceId,
                string.IsNullOrWhiteSpace(pair.PeerDeviceName) ? "已配对 Android 设备" : pair.PeerDeviceName,
                label),
        ];
    }
}
