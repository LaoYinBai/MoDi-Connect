using System;
using System.Net.NetworkInformation;

namespace MoDi.Desktop.Network;

public sealed class SystemNetworkChangeSource : INetworkChangeSource
{
    private readonly object _gate = new();
    private bool _disposed;

    public SystemNetworkChangeSource()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public event EventHandler? Changed;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            Changed = null;
        }

        GC.SuppressFinalize(this);
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs eventArgs) => RaiseChanged();

    private void OnNetworkAvailabilityChanged(
        object? sender,
        NetworkAvailabilityEventArgs eventArgs) => RaiseChanged();

    private void RaiseChanged()
    {
        EventHandler? changed;
        lock (_gate)
        {
            if (_disposed)
                return;
            changed = Changed;
        }

        changed?.Invoke(this, EventArgs.Empty);
    }
}
