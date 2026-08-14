using MoDi.Desktop.Core.Session;
using MoDi.Desktop.Links;
using MoDi.Protocol;
using Xunit;

namespace MoDi.Desktop.Tests.Links;

public sealed class SessionSwitchCoordinatorTests
{
    [Fact]
    public void Link_manager_publishes_none_and_disconnected_for_current_request()
    {
        using var manager = new LinkManager(managePhysicalLinks: false);
        var sessionId = Guid.NewGuid();
        manager.AcceptSession(LinkType.Bluetooth, sessionId);
        var request = SessionControlMessage.Request(
            sessionId,
            LinkType.Bluetooth,
            LinkType.Usb,
            DisconnectReason.UserSwitch);

        var ack = manager.AcceptDisconnect(request, LinkType.Bluetooth);

        Assert.Equal(DisconnectResult.Accepted, ack.Result);
        Assert.Equal("none", manager.ActiveLinkType);
        Assert.Equal(ConnectionState.Disconnected, manager.StateManager.State);
    }

    [Fact]
    public void Late_old_link_end_cannot_disconnect_the_new_session()
    {
        using var manager = new LinkManager(managePhysicalLinks: false);
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        manager.AcceptSession(LinkType.Usb, oldId);
        manager.AcceptSession(LinkType.WifiLan, newId);

        var ended = manager.EndSession(LinkType.Usb, oldId);

        Assert.False(ended);
        Assert.Equal("lan", manager.ActiveLinkType);
        Assert.Equal(newId, manager.CurrentSession?.SessionId);
        Assert.NotEqual(ConnectionState.Disconnected, manager.StateManager.State);
    }

    [Fact]
    public void Old_disconnect_cannot_end_new_session()
    {
        var coordinator = new SessionSwitchCoordinator();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        coordinator.Activate(LinkType.WifiLan, oldId);
        coordinator.Activate(LinkType.Usb, newId);

        var decision = coordinator.HandleDisconnect(
            SessionControlMessage.Request(oldId, LinkType.WifiLan, LinkType.Usb, DisconnectReason.UserSwitch),
            LinkType.WifiLan);

        Assert.False(decision.Accepted);
        Assert.Equal(DisconnectResult.Ignored, decision.Ack.Result);
        Assert.Equal(newId, coordinator.Current?.SessionId);
    }

    [Fact]
    public void Current_disconnect_is_accepted_and_repeated_request_is_idempotent()
    {
        var coordinator = new SessionSwitchCoordinator();
        var sessionId = Guid.NewGuid();
        var active = coordinator.Activate(LinkType.Bluetooth, sessionId);
        var request = SessionControlMessage.Request(
            sessionId,
            LinkType.Bluetooth,
            LinkType.WifiDirect,
            DisconnectReason.UserSwitch);

        var first = coordinator.HandleDisconnect(request, LinkType.Bluetooth);
        var repeated = coordinator.HandleDisconnect(request, LinkType.Bluetooth);

        Assert.True(first.Accepted);
        Assert.Equal(active, first.Ended);
        Assert.Equal(DisconnectResult.Accepted, first.Ack.Result);
        Assert.Null(coordinator.Current);
        Assert.True(repeated.Accepted);
        Assert.Null(repeated.Ended);
        Assert.Equal(DisconnectResult.Accepted, repeated.Ack.Result);
    }

    [Fact]
    public void Received_link_must_match_the_request_and_current_session()
    {
        var coordinator = new SessionSwitchCoordinator();
        var sessionId = Guid.NewGuid();
        coordinator.Activate(LinkType.Usb, sessionId);
        var request = SessionControlMessage.Request(
            sessionId,
            LinkType.Usb,
            LinkType.WifiLan,
            DisconnectReason.UserSwitch);

        var decision = coordinator.HandleDisconnect(request, LinkType.Bluetooth);

        Assert.False(decision.Accepted);
        Assert.Equal(sessionId, coordinator.Current?.SessionId);
    }

    [Fact]
    public void New_hello_replaces_the_previous_session_and_advances_generation()
    {
        var coordinator = new SessionSwitchCoordinator();

        var first = coordinator.Activate(LinkType.WifiLan, Guid.NewGuid());
        var second = coordinator.Activate(LinkType.WifiDirect, Guid.NewGuid());

        Assert.Equal(first.Generation + 1, second.Generation);
        Assert.Equal(second, coordinator.Current);
        Assert.False(coordinator.EndIfCurrent(first.LinkType, first.SessionId));
        Assert.True(coordinator.EndIfCurrent(second.LinkType, second.SessionId));
        Assert.Null(coordinator.Current);
    }
}
