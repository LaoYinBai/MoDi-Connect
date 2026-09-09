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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoDi.Desktop.Connectivity.Receiver;

namespace MoDi.Desktop.Services;

/// <summary>
/// Windows 接收端的应用级 UI 门面。只编排启动/刷新/明确连接，
/// 链路生命周期和状态投影分别由对应 owner 管理。
/// </summary>
public sealed class ReceiverController : IDisposable
{
    private readonly ReceiverLifecycleCoordinator _lifecycle;
    private readonly ReceiverSnapshotProjection _snapshot;
    private bool _disposed;

    internal ReceiverController(IReceiverLinkRuntime links)
    {
        _lifecycle = new ReceiverLifecycleCoordinator(links);
        _snapshot = new ReceiverSnapshotProjection(links);
        _snapshot.Changed += OnSnapshotChanged;
        _snapshot.QrPayloadChanged += OnQrPayloadChanged;
    }

    public event Action? SnapshotChanged;
    public event Action<string?, string?>? QrPayloadChanged;

    public ConnectionState ConnectionState => _snapshot.ConnectionState;
    public string ActiveLink => _snapshot.ActiveLink;
    public int CurrentRoute => _snapshot.CurrentRoute;
    public string StatusMessage => _snapshot.StatusMessage;
    public string LastError => _snapshot.LastError;
    public string LanStatus => _snapshot.LanStatus;
    public string P2pStatus => _snapshot.P2pStatus;
    public string BluetoothStatus => _snapshot.BluetoothStatus;
    public string UsbStatus => _snapshot.UsbStatus;
    public bool IsP2pProgressVisible => _snapshot.IsP2pProgressVisible;
    public bool IsP2pProgressIndeterminate => _snapshot.IsP2pProgressIndeterminate;
    public double P2pProgress => _snapshot.P2pProgress;
    public double Volume { get => _snapshot.Volume; set => _snapshot.Volume = value; }
    public IReadOnlyList<P2pCandidateInfo> P2pCandidates => _snapshot.P2pCandidates;

    public async Task InitializeAsync()
    {
        var result = await _lifecycle.InitializeAsync();
        _snapshot.ApplyStartupResult(result);
    }

    public async Task RefreshP2pAsync()
    {
        _snapshot.BeginP2pRestart("正在刷新 P2P 二维码...");
        _snapshot.ApplyP2pError(await _lifecycle.RestartP2pAsync());
    }

    public async Task ConnectRecentP2pAsync()
    {
        _snapshot.BeginP2pRestart("正在重新等待已配对设备...");
        _snapshot.ApplyP2pError(await _lifecycle.RestartP2pAsync());
    }

    public Task ConnectP2pCandidateAsync(string deviceId)
    {
        if (!_lifecycle.ConnectP2pCandidate(deviceId))
            throw new InvalidOperationException("目标设备未在当前被动发现列表中，请稍后重试");
        return Task.CompletedTask;
    }

    public PairedDeviceStore.PairedInfo? GetRecentPair() => PairedDeviceStore.Load();
    public IReadOnlyList<P2pCandidateInfo> GetP2pCandidates() => P2pCandidates;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _snapshot.Changed -= OnSnapshotChanged;
        _snapshot.QrPayloadChanged -= OnQrPayloadChanged;
        _snapshot.Dispose();
        _lifecycle.Dispose();
    }

    private void OnSnapshotChanged() => SnapshotChanged?.Invoke();
    private void OnQrPayloadChanged(string? payload, string? deviceName) =>
        QrPayloadChanged?.Invoke(payload, deviceName);
}
