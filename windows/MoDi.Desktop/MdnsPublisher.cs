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
using System.Threading;
using System.Threading.Tasks;
using Makaretu.Dns;
using MoDi.Core.Infrastructure;
using MoDi.Desktop.Diagnostics;
using MoDi.Desktop.Network;

namespace MoDi.Desktop;

/// <summary>
/// mDNS 服务发布 — 启动时注册 _modi._udp 服务
/// 关闭时注销，供 Android NsdManager 自动发现
/// </summary>
public sealed class MdnsPublisher : IDisposable
{
    private static readonly TimeSpan NetworkChangeDebounce = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
    ];

    private readonly IMdnsAdvertiser _advertiser;
    private readonly INetworkChangeSource _networkChanges;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _stateGate = new();
    private CancellationTokenSource _lifetimeCts = new();
    private CancellationTokenSource? _debounceCts;
    private bool _running;
    private bool _desiredRunning;
    private bool _disposed;
    private long _generation;

    /// <param name="hostname">电脑名称，Android 端以此识别设备</param>
    /// <param name="port">音频数据端口，与 AudioEngine.Port 一致</param>
    public MdnsPublisher(string hostname, int port)
        : this(
            hostname,
            port,
            static (name, serviceType, servicePort) =>
                new ServiceProfile(name, serviceType, servicePort)) { }

    internal MdnsPublisher(
        string hostname,
        int port,
        Func<string, string, ushort, ServiceProfile> createProfile)
        : this(
            new MakaretuMdnsAdvertiser(hostname, port, createProfile),
            new SystemNetworkChangeSource(),
            TimeProvider.System) { }

    internal MdnsPublisher(
        IMdnsAdvertiser advertiser,
        INetworkChangeSource networkChanges,
        TimeProvider timeProvider)
    {
        _advertiser = advertiser ?? throw new ArgumentNullException(nameof(advertiser));
        _networkChanges = networkChanges ?? throw new ArgumentNullException(nameof(networkChanges));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _networkChanges.Changed += OnNetworkChanged;
    }

    /// <summary>工厂方法，自动使用当前机器名</summary>
    public static MdnsPublisher Create(string hostname, int port) => new(hostname, port);

    internal bool IsRunning
    {
        get
        {
            lock (_stateGate)
                return _running;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        long generation;
        CancellationToken lifetimeToken;
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_desiredRunning)
            {
                _desiredRunning = true;
                if (_lifetimeCts.IsCancellationRequested)
                {
                    _lifetimeCts.Dispose();
                    _lifetimeCts = new CancellationTokenSource();
                }
                _generation++;
            }

            generation = _generation;
            lifetimeToken = _lifetimeCts.Token;
        }

        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetimeToken);

        var entered = false;
        try
        {
            await _lifecycleGate.WaitAsync(operationCts.Token).ConfigureAwait(false);
            entered = true;
            if (!IsCurrent(generation, operationCts.Token) || IsRunning)
                return;

            await StartWithRetryAsync(generation, operationCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            lifetimeToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Stop/Dispose owns this cancellation; it is an expected lifecycle transition.
        }
        finally
        {
            if (entered)
                _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource lifetimeCts;
        CancellationTokenSource? debounceCts;
        lock (_stateGate)
        {
            if (_disposed)
                return;

            _desiredRunning = false;
            _generation++;
            lifetimeCts = _lifetimeCts;
            debounceCts = _debounceCts;
            _debounceCts = null;
        }

        lifetimeCts.Cancel();
        debounceCts?.Cancel();

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopCore();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource lifetimeCts;
        CancellationTokenSource? debounceCts;
        lock (_stateGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _desiredRunning = false;
            _generation++;
            lifetimeCts = _lifetimeCts;
            debounceCts = _debounceCts;
            _debounceCts = null;
        }

        _networkChanges.Changed -= OnNetworkChanged;
        lifetimeCts.Cancel();
        debounceCts?.Cancel();

        _lifecycleGate.Wait();
        try
        {
            StopCore();
        }
        finally
        {
            _lifecycleGate.Release();
        }

        DisposeSafely(_advertiser, "advertiser");
        DisposeSafely(_networkChanges, "network change source");
        debounceCts?.Dispose();
        lifetimeCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnNetworkChanged(object? sender, EventArgs eventArgs)
    {
        CancellationTokenSource debounceCts;
        long generation;
        lock (_stateGate)
        {
            if (_disposed || !_desiredRunning)
                return;

            _debounceCts?.Cancel();
            debounceCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            _debounceCts = debounceCts;
            generation = _generation;
        }

        var restart = RestartAfterDebounceAsync(generation, debounceCts);
        GlobalExceptionBoundary.Observe(restart, "MdnsPublisher.NetworkChangeRestart");
    }

    private async Task RestartAfterDebounceAsync(
        long generation,
        CancellationTokenSource debounceCts)
    {
        var entered = false;
        try
        {
            await Task.Delay(NetworkChangeDebounce, _timeProvider, debounceCts.Token)
                .ConfigureAwait(false);
            await _lifecycleGate.WaitAsync(debounceCts.Token).ConfigureAwait(false);
            entered = true;
            if (!IsCurrent(generation, debounceCts.Token))
                return;

            StopCore();
            await StartWithRetryAsync(generation, debounceCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (debounceCts.IsCancellationRequested)
        {
            // A newer network event, Stop, or Dispose owns this cancellation.
        }
        finally
        {
            if (entered)
                _lifecycleGate.Release();

            lock (_stateGate)
            {
                if (ReferenceEquals(_debounceCts, debounceCts))
                    _debounceCts = null;
            }
            debounceCts.Dispose();
        }
    }

    private async Task StartWithRetryAsync(long generation, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < RetryDelays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation, cancellationToken))
                return;

            try
            {
                _advertiser.Start();
                _advertiser.Advertise();
                if (TryMarkRunning(generation, cancellationToken))
                    return;

                StopAdvertiserAfterFailedStart();
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lock (_stateGate)
                    _running = false;
                Log.E("MdnsPublisher", $"Start failed: {ex.Message}");
                StopAdvertiserAfterFailedStart();
            }

            await Task.Delay(RetryDelays[attempt], _timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private bool TryMarkRunning(long generation, CancellationToken cancellationToken)
    {
        lock (_stateGate)
        {
            if (_disposed || !_desiredRunning || _generation != generation ||
                cancellationToken.IsCancellationRequested)
                return false;

            _running = true;
            return true;
        }
    }

    private bool IsCurrent(long generation, CancellationToken cancellationToken)
    {
        lock (_stateGate)
            return !_disposed && _desiredRunning && _generation == generation &&
                !cancellationToken.IsCancellationRequested;
    }

    private void StopCore()
    {
        lock (_stateGate)
        {
            if (!_running)
                return;
            _running = false;
        }

        try
        {
            _advertiser.Stop();
        }
        catch (Exception ex)
        {
            Log.E("MdnsPublisher", $"Stop failed: {ex.Message}");
        }
    }

    private void StopAdvertiserAfterFailedStart()
    {
        try
        {
            _advertiser.Stop();
        }
        catch (Exception ex)
        {
            Log.E("MdnsPublisher", $"Failed-start cleanup failed: {ex.Message}");
        }
    }

    private static void DisposeSafely(IDisposable disposable, string component)
    {
        try
        {
            disposable.Dispose();
        }
        catch (Exception ex)
        {
            Log.E("MdnsPublisher", $"Dispose {component} failed: {ex.Message}");
        }
    }

}

internal interface IMdnsAdvertiser : IDisposable
{
    void Start();
    void Advertise();
    void Stop();
}

internal sealed class MakaretuMdnsAdvertiser : IMdnsAdvertiser
{
    private readonly MulticastService _mdns = new();
    private readonly ServiceDiscovery _serviceDiscovery;
    private readonly ServiceProfile _profile;

    public MakaretuMdnsAdvertiser(
        string hostname,
        int port,
        Func<string, string, ushort, ServiceProfile> createProfile)
    {
        _serviceDiscovery = new ServiceDiscovery(_mdns);
        _profile = createProfile(
            hostname,
            TransportIdentity.MdnsServiceType,
            checked((ushort)port));
    }

    public void Start() => _mdns.Start();

    public void Advertise() => _serviceDiscovery.Advertise(_profile);

    public void Stop()
    {
        try
        {
            _serviceDiscovery.Unadvertise(_profile);
        }
        finally
        {
            _mdns.Stop();
        }
    }

    public void Dispose()
    {
        try
        {
            _serviceDiscovery.Dispose();
        }
        finally
        {
            _mdns.Dispose();
        }
    }
}
