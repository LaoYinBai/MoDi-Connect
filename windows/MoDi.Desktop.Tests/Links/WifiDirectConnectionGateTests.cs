using MoDi.Desktop.Links;
using Xunit;

namespace MoDi.Desktop.Tests.Links;

public sealed class WifiDirectConnectionGateTests
{
    [Fact]
    public void Unknown_candidates_remain_passive_until_the_user_selects_one()
    {
        var gate = new WifiDirectConnectionGate(trustedDeviceId: null);

        var first = gate.ObserveCandidate("device-a", "普通手机 A");
        var second = gate.ObserveCandidate("device-b", "普通手机 B");

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(2, gate.Candidates.Count);
    }

    [Fact]
    public void Explicit_selection_authorizes_only_the_observed_target()
    {
        var gate = new WifiDirectConnectionGate(trustedDeviceId: null);
        gate.ObserveCandidate("device-a", "目标手机");

        var rejected = gate.AuthorizeExplicit("device-b");
        var accepted = gate.AuthorizeExplicit("device-a");

        Assert.Null(rejected);
        Assert.Equal("device-a", accepted?.DeviceId);
        Assert.Equal(WifiDirectConnectionReason.ExplicitUserSelection, accepted?.Reason);
    }

    [Fact]
    public void Only_the_exact_trusted_device_can_trigger_automatic_reconnect()
    {
        var gate = new WifiDirectConnectionGate("trusted-device");

        var unknown = gate.ObserveCandidate("unknown-device", "同型号手机");
        var trusted = gate.ObserveCandidate("trusted-device", "已配对手机");

        Assert.Null(unknown);
        Assert.Equal("trusted-device", trusted?.DeviceId);
        Assert.Equal(WifiDirectConnectionReason.TrustedReconnect, trusted?.Reason);
    }

    [Fact]
    public void Missing_trusted_device_never_falls_back_to_an_unknown_candidate()
    {
        var gate = new WifiDirectConnectionGate("offline-trusted-device");

        var candidate = gate.ObserveCandidate("nearby-unknown-device", "附近手机");

        Assert.Null(candidate);
        Assert.Null(gate.AuthorizeTrusted("nearby-unknown-device"));
    }

    [Fact]
    public void Repeated_discovery_updates_one_candidate_instead_of_duplicating_it()
    {
        var gate = new WifiDirectConnectionGate(trustedDeviceId: null);

        gate.ObserveCandidate("device-a", "旧名称");
        gate.ObserveCandidate("device-a", "新名称");

        var candidate = Assert.Single(gate.Candidates);
        Assert.Equal("新名称", candidate.DisplayName);
    }

    [Fact]
    public void Candidate_that_leaves_discovery_is_no_longer_authorized_for_explicit_connection()
    {
        var gate = new WifiDirectConnectionGate(trustedDeviceId: null);
        gate.ObserveCandidate("device-a", "附近手机");

        var removed = gate.RemoveCandidate("device-a");

        Assert.True(removed);
        Assert.Empty(gate.Candidates);
        Assert.Null(gate.AuthorizeExplicit("device-a"));
    }
}
