using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Adapters;

public sealed class PairingAdapter : IPairingService
{
    private const string RecentDeviceId = "recent-p2p";
    private const string CandidatePrefix = "candidate:";
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
        var isRecent = deviceId == RecentDeviceId;
        var candidateId = deviceId.StartsWith(CandidatePrefix, StringComparison.Ordinal)
            ? deviceId[CandidatePrefix.Length..]
            : null;
        if (!isRecent && candidateId is null)
            return OperationResult.Failure("PAIR_DEVICE_NOT_FOUND", "找不到可连接的目标设备");
        if (isRecent && !IsTrustedPair(_runtime.GetRecentPair()))
            return OperationResult.Failure("PAIR_DEVICE_NOT_FOUND", "找不到可重新连接的配对设备");
        if (candidateId is not null && !_runtime.GetP2pCandidates().Any(candidate => candidate.DeviceId == candidateId))
            return OperationResult.Failure("PAIR_DEVICE_NOT_FOUND", "目标设备已离开被动发现列表");

        try
        {
            if (isRecent)
                await _runtime.ConnectRecentP2pAsync();
            else
                await _runtime.ConnectP2pCandidateAsync(candidateId!);
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
        var devices = new List<PairedDeviceSnapshot>();
        if (IsTrustedPair(pair))
        {
            devices.Add(new PairedDeviceSnapshot(
                RecentDeviceId,
                string.IsNullOrWhiteSpace(pair!.PeerDeviceName) ? "已配对 Android 设备" : pair.PeerDeviceName,
                $"上次连接：{pair.LastConnected:yyyy-MM-dd HH:mm}"));
        }

        foreach (var candidate in _runtime.GetP2pCandidates())
        {
            if (pair?.P2pDeviceId == candidate.DeviceId)
                continue;
            devices.Add(new PairedDeviceSnapshot(
                CandidatePrefix + candidate.DeviceId,
                candidate.DisplayName,
                "附近设备 · 点击后才会发起连接"));
        }
        return devices;
    }

    private static bool IsTrustedPair(PairedDeviceStore.PairedInfo? pair) =>
        pair is not null &&
        pair.LastConnected != DateTime.MinValue &&
        !string.IsNullOrWhiteSpace(pair.P2pDeviceId);
}
