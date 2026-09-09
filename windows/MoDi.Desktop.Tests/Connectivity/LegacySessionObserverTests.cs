using MoDi.App.Contracts.Connectivity;
using MoDi.Desktop.Connectivity.Sessions;
using Xunit;

namespace MoDi.Desktop.Tests.Connectivity;

public sealed class LegacySessionObserverTests
{
    [Fact]
    public void Observer_projects_only_the_current_legacy_session()
    {
        var registry = new SessionRegistry();
        var observer = new LegacySessionObserver(registry);
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        observer.ObserveState(ConnectionState.Streaming);
        observer.ObserveStarted(id, TransportKind.Lan, ConnectionState.Connecting);
        observer.ObserveState(ConnectionState.Connected);
        observer.ObserveState(ConnectionState.Streaming);
        observer.ObserveEnded(id);

        var snapshot = Assert.Single(registry.Snapshots);
        Assert.Equal(SessionState.Closed, snapshot.State);
        Assert.DoesNotContain(typeof(LegacySessionObserver).GetMethods(), method =>
            method.Name.Contains("Connect") || method.Name.Contains("Send") || method.Name.Contains("Disconnect"));
    }

    [Fact]
    public void Production_shadow_gate_is_off_by_default()
    {
        Assert.Null(SessionShadowComposition.CreateIfEnabled(_ => null));
    }
}
