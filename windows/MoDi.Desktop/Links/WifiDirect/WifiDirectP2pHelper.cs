/*
 * MoDi Connect - Cross-device interconnection protocol
 * Copyright (C) 2026 Silvite
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using MoDi.Core.Infrastructure;

namespace MoDi.Desktop.Links;

/// <summary>
/// WifiDirectP2pHelper — WiFi Direct P2P 建链辅助器（Windows 做客户端，连接 Android GO）
///
/// 职责：
/// 1. 生成 QR 码内容（device name + token）
/// 2. DeviceWatcher 持续监听 Android P2P 设备
/// 3. WiFiDirectDevice.FromIdAsync() 连接到 Android GO
/// 4. 轮询 P2P 适配器获取本机 IP
///
/// 角色：Android 做 GO（固定 IP 192.168.49.1），Windows 做客户端（DHCP 获取 192.168.49.x）。
/// </summary>
public sealed class WifiDirectP2pHelper : IDisposable
{
    private const string Tag = "WifiDirectP2pHelper";
    private const int ProgressReportIntervalMs = 5_000;
    private const int ConnectTimeoutMs = 15_000;
    private const int MaxConnectRetries = 3;

    // ── P2P 链路常量（自包含，不依赖 WifiDirectLink） ──
    public const string GoIp = "192.168.49.1";
    public const int DiscoverTimeoutMs = 120_000;
    public const int IpPollIntervalMs = 500;
    public const int IpPollMaxRetries = 30;

    // ── 公开属性（供 UI / QR 码使用） ──
    public string DeviceName { get; }
    public string Token { get; }
    public string? LocalIp { get; private set; }
    public bool IsConnected { get; private set; }
    public string? ConnectedDeviceId { get; private set; }

    /// <summary>P2P 连接就绪（已连接到 Android GO），可发起握手</summary>
    public event Action? OnConnected;

    /// <summary>P2P 连接丢失（Android 断开 / Group 销毁）</summary>
    public event Action? OnDisconnected;

    /// <summary>进度状态变化（UI 订阅显示进度文字）</summary>
    public event Action<string>? OnStatusChanged;
    internal event Action<IReadOnlyList<WifiDirectCandidate>>? OnCandidatesChanged;

    // ── 内部 ──
    private WiFiDirectDevice? _device;
    private DeviceWatcher? _watcher;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<AuthorizedWifiDirectTarget?>? _connectionRequestTcs;
    private TaskCompletionSource? _connectionLostTcs;
    private readonly WifiDirectConnectionGate _connectionGate;

    public WifiDirectP2pHelper()
    {
        // 使用持久化 token（跨会话不变），实现免扫码重连
        var paired = PairedDeviceStore.GetOrCreate();
        DeviceName = paired.DeviceName;
        Token = paired.Token;
        var trustedDeviceId = paired.LastConnected != DateTime.MinValue
            ? paired.P2pDeviceId
            : null;
        _connectionGate = new WifiDirectConnectionGate(trustedDeviceId);
    }

    /// <summary>启动 P2P 持久监听循环（发现→连接→等待断开→重新发现，直到 StopAsync）</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_cts != null) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        Log.I(Tag, $"P2P started. Device={DeviceName}, credential=<redacted>");

        // 持久循环：连接断开后自动重新发现
        while (!token.IsCancellationRequested)
        {
            try
            {
                ReportStatus("正在被动发现手机；选择目标或扫码后连接");

                // Discovery only publishes candidates. It may authorize the exact previously
                // authenticated id, but never probes an unknown endpoint by connecting to it.
                var target = await WaitForAuthorizedTargetAsync(token);
                if (target == null)
                {
                    ReportStatus("暂未发现可连接目标，继续被动发现...");
                    continue;
                }

                // ── Phase 2: 连接设备（含重试） ──
                StopWatcher();
                var connected = await ConnectWithRetryAsync(target, token);
                if (!connected)
                {
                    ReportStatus("P2P 连接失败（已重试 3 次），重新等待...");
                    Log.W(Tag, "P2P connection failed after retries, restarting loop");
                    continue;
                }
                ConnectedDeviceId = target.DeviceId;

                // ── Phase 3: 获取 P2P 适配器 IP ──
                ReportStatus("P2P 已连接，获取 IP...");
                var ip = await PollForP2pIpAsync(token);
                if (ip == null)
                {
                    ReportStatus("P2P 适配器未获取 IP（15s 超时），重新等待...");
                    Log.W(Tag, "P2P adapter IP not found (15s), restarting loop");
                    CleanupDevice();
                    ConnectedDeviceId = null;
                    continue;
                }

                LocalIp = ip;
                IsConnected = true;
                ReportStatus($"P2P 就绪 ✓ local={ip} go={GoIp}");
                Log.I(Tag, $"P2P ready: local={ip}, GO={GoIp}");

                OnConnected?.Invoke();

                // ── Phase 4: 等待连接丢失 ──
                _connectionLostTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var lostReg = token.Register(() => _connectionLostTcs?.TrySetCanceled());
                await _connectionLostTcs.Task;

                // 连接丢失，清理后重新进入发现循环
                Log.I(Tag, "Connection lost, restarting discovery loop...");
                ReportStatus("P2P 连接断开，重新等待手机...");
                IsConnected = false;
                LocalIp = null;
                ConnectedDeviceId = null;
                CleanupDevice();
                OnDisconnected?.Invoke();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ReportStatus($"错误：{ex.Message}");
                Log.E(Tag, $"StartAsync loop error: {ex}");
                CleanupDevice();
                // 出错后等 3s 再重试，避免快速循环
                try { await Task.Delay(3_000, token); } catch (OperationCanceledException) { break; }
            }
        }

        StopWatcher();
        Log.I(Tag, "P2P loop exited");
    }

    /// <summary>停止 P2P，释放所有资源，退出持久循环</summary>
    public Task StopAsync()
    {
        _cts?.Cancel();
        _connectionLostTcs?.TrySetCanceled();
        StopWatcher();
        CleanupDevice();

        IsConnected = false;
        LocalIp = null;
        ConnectedDeviceId = null;
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    private void CleanupDevice()
    {
        if (_device != null)
        {
            _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _device.Dispose();
            _device = null;
        }
    }

    // ── P1.1 + P1.2: 设备发现（FindAllAsync + DeviceWatcher 双模式） ──

    /// <summary>
    /// 发现 P2P 设备：先用 FindAllAsync 快速扫描，找不到再启动 DeviceWatcher 持续监听。
    /// 解决 DeviceWatcher 二次启动时不触发 Added 事件的问题。
    /// </summary>
    private async Task<AuthorizedWifiDirectTarget?> WaitForAuthorizedTargetAsync(CancellationToken ct)
    {
        _connectionGate.ClearCandidates();
        OnCandidatesChanged?.Invoke(_connectionGate.Candidates);
        _connectionRequestTcs = new TaskCompletionSource<AuthorizedWifiDirectTarget?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var selector = WiFiDirectDevice.GetDeviceSelector(WiFiDirectDeviceSelectorType.AssociationEndpoint);

        // ── 快速扫描：FindAllAsync 一次性枚举当前可见设备 ──
        try
        {
            Log.I(Tag, "[Discovery] Trying FindAllAsync first...");
            var devices = await DeviceInformation.FindAllAsync(selector).AsTask(ct);
            foreach (var device in devices)
            {
                var authorized = ObserveCandidate(device);
                if (authorized is not null)
                {
                    Log.I(Tag, "Discovery: trusted peer matched");
                    return authorized;
                }
            }
            Log.I(Tag, $"Discovery: FindAll returned {devices.Count} passive candidate(s)");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.W(Tag, $"[Discovery] FindAllAsync error: {ex.Message}");
        }

        // ── 持续监听：DeviceWatcher 等待新设备出现 ──
        return await WaitWithWatcherAsync(selector, ct);
    }

    private async Task<AuthorizedWifiDirectTarget?> WaitWithWatcherAsync(string selector, CancellationToken ct)
    {
        var request = _connectionRequestTcs
            ?? throw new InvalidOperationException("Connection request queue was not initialized");
        _watcher = DeviceInformation.CreateWatcher(selector);

        int elapsed = 0;
        var progressTimer = new Timer(_ =>
        {
            elapsed += ProgressReportIntervalMs;
            ReportStatus($"等待手机创建 P2P... {elapsed / 1000}s");
        }, null, ProgressReportIntervalMs, ProgressReportIntervalMs);

        _watcher.Added += OnWatcherAdded;
        _watcher.Updated += OnWatcherUpdated;
        _watcher.Removed += OnWatcherRemoved;
        _watcher.EnumerationCompleted += OnWatcherEnumerationCompleted;
        _watcher.Stopped += OnWatcherStopped;

        try
        {
            _watcher.Start();
            Log.I(Tag, "[Watcher] Started, waiting for Android P2P device...");

            // 等待用户授权、精确可信设备出现，或发现周期超时。
            using var reg = ct.Register(() => request.TrySetCanceled(ct));
            var timeoutTask = Task.Delay(DiscoverTimeoutMs, ct);
            var completedTask = await Task.WhenAny(request.Task, timeoutTask);

            if (completedTask == timeoutTask)
                return null;

            ct.ThrowIfCancellationRequested();
            return await request.Task;
        }
        finally
        {
            progressTimer.Dispose();
            StopWatcher();
        }
    }

    public bool RequestExplicitConnection(string deviceId)
    {
        var target = _connectionGate.AuthorizeExplicit(deviceId);
        if (target is null)
        {
            Log.W(Tag, "Connect: blocked because target is unconfirmed");
            return false;
        }

        Log.I(Tag, "Connect: explicit user target");
        return _connectionRequestTcs?.TrySetResult(target) == true;
    }

    private AuthorizedWifiDirectTarget? ObserveCandidate(DeviceInformation device)
    {
        var target = _connectionGate.ObserveCandidate(device.Id, device.Name);
        OnCandidatesChanged?.Invoke(_connectionGate.Candidates);
        Log.I(Tag, target is null
            ? "Discovery: untrusted candidate recorded without connecting"
            : "Discovery: trusted reconnect target authorized");
        return target;
    }

    private void OnWatcherAdded(DeviceWatcher sender, DeviceInformation device)
    {
        var target = ObserveCandidate(device);
        if (target is not null)
            _connectionRequestTcs?.TrySetResult(target);
    }

    private void OnWatcherUpdated(DeviceWatcher sender, DeviceInformationUpdate update) =>
        Log.D(Tag, "Discovery: candidate metadata updated");

    private void OnWatcherRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (!_connectionGate.RemoveCandidate(update.Id))
            return;

        OnCandidatesChanged?.Invoke(_connectionGate.Candidates);
        Log.I(Tag, "Discovery: candidate left range and was removed");
    }

    private void OnWatcherEnumerationCompleted(DeviceWatcher sender, object args) =>
        Log.I(Tag, "Discovery: watcher enumeration completed");

    private void OnWatcherStopped(DeviceWatcher sender, object args) =>
        Log.I(Tag, "Discovery: watcher stopped");

    // ── P1.3 + P1.5: 连接 + 重试 + ConnectionStatusChanged ──

    private async Task<bool> ConnectWithRetryAsync(AuthorizedWifiDirectTarget target, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxConnectRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            ReportStatus($"正在连接 P2P 设备...（尝试 {attempt}/{MaxConnectRetries}）");
            Log.I(Tag, $"Connect: entering Wi-Fi Direct API, reason={target.Reason}, attempt={attempt}/{MaxConnectRetries}");

            try
            {
                // 清理上一次失败的设备实例
                if (_device != null)
                {
                    _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    _device.Dispose();
                    _device = null;
                }

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(ConnectTimeoutMs);

                _device = await WiFiDirectDevice.FromIdAsync(target.DeviceId).AsTask(connectCts.Token);

                if (_device != null && _device.ConnectionStatus == WiFiDirectConnectionStatus.Connected)
                {
                    _device.ConnectionStatusChanged += OnConnectionStatusChanged;
                    Log.I(Tag, $"P2P connected successfully on attempt {attempt}");
                    return true;
                }

                Log.W(Tag, $"Attempt {attempt}: device returned but status={_device?.ConnectionStatus}");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.W(Tag, $"Attempt {attempt}: connection timeout ({ConnectTimeoutMs}ms)");
            }
            catch (Exception ex)
            {
                Log.W(Tag, $"Attempt {attempt}: {ex.GetType().Name}: {ex.Message}");
            }

            // 重试前等待 2s（避免立即重试导致驱动未就绪）
            if (attempt < MaxConnectRetries)
            {
                ReportStatus($"连接失败，{2}s 后重试...");
                await Task.Delay(2_000, ct);
            }
        }

        return false;
    }

    private void OnConnectionStatusChanged(WiFiDirectDevice sender, object args)
    {
        var status = sender.ConnectionStatus;
        Log.I(Tag, $"[P2P] ConnectionStatus changed: {status}");

        if (status != WiFiDirectConnectionStatus.Connected && IsConnected)
        {
            IsConnected = false;
            ReportStatus("P2P 连接已断开");
            Log.W(Tag, "P2P connection lost");
            // 通知持久循环：连接丢失，重新进入发现
            _connectionLostTcs?.TrySetResult();
        }
    }

    // ── P1.4: 轮询 P2P 适配器 IP ──

    private async Task<string?> PollForP2pIpAsync(CancellationToken ct)
    {
        for (int i = 0; i < IpPollMaxRetries; i++)
        {
            ct.ThrowIfCancellationRequested();
            var ip = GetP2pAdapterIp();
            if (ip != null)
            {
                Log.I(Tag, $"P2P adapter IP found: {ip} (poll #{i + 1})");
                return ip;
            }
            await Task.Delay(IpPollIntervalMs, ct);
        }
        return null;
    }

    private static string? GetP2pAdapterIp()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            var desc = ni.Description;
            if (!desc.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase)
                && !desc.Contains("P2P", StringComparison.OrdinalIgnoreCase)
                && !desc.Contains("Microsoft Wi-Fi Direct", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ni.OperationalStatus != OperationalStatus.Up) continue;

            foreach (var ip in ni.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork
                    && !ip.Address.ToString().StartsWith("169.254"))
                    return ip.Address.ToString();
            }
        }
        return null;
    }

    // ── 工具方法 ──

    private void StopWatcher()
    {
        if (_watcher != null)
        {
            try
            {
                if (_watcher.Status == DeviceWatcherStatus.Started ||
                    _watcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                {
                    _watcher.Stop();
                }
            }
            catch (Exception ex)
            {
                Log.W(Tag, $"StopWatcher error: {ex.Message}");
            }
            _watcher.Added -= OnWatcherAdded;
            _watcher.Updated -= OnWatcherUpdated;
            _watcher.Removed -= OnWatcherRemoved;
            _watcher.EnumerationCompleted -= OnWatcherEnumerationCompleted;
            _watcher.Stopped -= OnWatcherStopped;
            _watcher = null;
        }
    }

    private void ReportStatus(string msg)
    {
        Log.I(Tag, msg);
        OnStatusChanged?.Invoke(msg);
    }

    public void Dispose()
    {
        _ = StopAsync();
    }
}
