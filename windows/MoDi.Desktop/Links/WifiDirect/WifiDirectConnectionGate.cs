using System;
using System.Collections.Generic;
using System.Linq;

namespace MoDi.Desktop.Links;

internal enum WifiDirectConnectionReason
{
    ExplicitUserSelection,
    TrustedReconnect,
}

internal sealed record WifiDirectCandidate(string DeviceId, string DisplayName);

internal sealed record AuthorizedWifiDirectTarget(
    string DeviceId,
    WifiDirectConnectionReason Reason);

/// <summary>
/// Separates passive discovery from the operation that may display remote system UI.
/// A discovered endpoint is not connectable until the user selects it, unless its exact
/// DeviceInformation id belongs to a previously authenticated peer.
/// </summary>
internal sealed class WifiDirectConnectionGate
{
    private readonly object _sync = new();
    private readonly string? _trustedDeviceId;
    private readonly Dictionary<string, WifiDirectCandidate> _candidates =
        new(StringComparer.Ordinal);

    public WifiDirectConnectionGate(string? trustedDeviceId) =>
        _trustedDeviceId = string.IsNullOrWhiteSpace(trustedDeviceId) ? null : trustedDeviceId;

    public IReadOnlyList<WifiDirectCandidate> Candidates
    {
        get { lock (_sync) return _candidates.Values.ToArray(); }
    }

    public AuthorizedWifiDirectTarget? ObserveCandidate(string deviceId, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return null;

        lock (_sync)
        {
            _candidates[deviceId] = new WifiDirectCandidate(
                deviceId,
                string.IsNullOrWhiteSpace(displayName) ? "未命名 Wi-Fi Direct 设备" : displayName);
        }

        return AuthorizeTrusted(deviceId);
    }

    public AuthorizedWifiDirectTarget? AuthorizeExplicit(string deviceId) =>
        !string.IsNullOrWhiteSpace(deviceId) && ContainsCandidate(deviceId)
            ? new AuthorizedWifiDirectTarget(deviceId, WifiDirectConnectionReason.ExplicitUserSelection)
            : null;

    public AuthorizedWifiDirectTarget? AuthorizeTrusted(string deviceId) =>
        _trustedDeviceId is not null && string.Equals(deviceId, _trustedDeviceId, StringComparison.Ordinal)
            ? new AuthorizedWifiDirectTarget(deviceId, WifiDirectConnectionReason.TrustedReconnect)
            : null;

    public void ClearCandidates()
    {
        lock (_sync) _candidates.Clear();
    }

    public bool RemoveCandidate(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;

        lock (_sync) return _candidates.Remove(deviceId);
    }

    private bool ContainsCandidate(string deviceId)
    {
        lock (_sync) return _candidates.ContainsKey(deviceId);
    }
}
