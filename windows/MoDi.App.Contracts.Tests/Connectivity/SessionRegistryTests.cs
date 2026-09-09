using System.Text.Json;
using MoDi.App.Contracts.Connectivity;

namespace MoDi.App.Contracts.Tests.Connectivity;

public sealed class SessionRegistryTests
{
    [Fact]
    public void Shadow_events_are_idempotent_and_never_create_control_actions()
    {
        var registry = new SessionRegistry();
        using var vectors = JsonDocument.Parse(File.ReadAllText(FindVectors()));
        foreach (var item in vectors.RootElement.GetProperty("sessionShadowEvents").EnumerateArray())
        {
            var id = SessionId.Parse(item.GetProperty("session").GetString()!);
            var transport = Enum.Parse<TransportKind>(item.GetProperty("transport").GetString()!);
            var state = Enum.Parse<SessionState>(item.GetProperty("state").GetString()!);
            switch (item.GetProperty("operation").GetString())
            {
                case "start": registry.ObserveStarted(id, transport, state); break;
                case "state": registry.ObserveState(id, state); break;
                case "end": registry.ObserveEnded(id); break;
                case "closeAll": registry.ObserveClosedAll(); break;
            }
        }

        Assert.Equal(2, registry.Snapshots.Count);
        Assert.All(registry.Snapshots, snapshot => Assert.Equal(SessionState.Closed, snapshot.State));
        Assert.DoesNotContain(typeof(SessionRegistry).GetMethods(), method =>
            method.Name.Contains("Connect") || method.Name.Contains("Send") || method.Name.Contains("Disconnect"));
    }

    [Fact]
    public void Unknown_or_illegal_state_event_is_reported_without_mutating_a_session()
    {
        var registry = new SessionRegistry();
        var missing = registry.ObserveState(SessionId.Parse("missing"), SessionState.Ready);
        registry.ObserveStarted(SessionId.Parse("known"), TransportKind.Lan, SessionState.Ready);
        var illegal = registry.ObserveState(SessionId.Parse("known"), SessionState.Authenticating);

        Assert.False(missing.Applied);
        Assert.False(illegal.Applied);
        Assert.Equal(SessionState.Ready, registry.Snapshots.Single(x => x.Id == SessionId.Parse("known")).State);
    }

    private static string FindVectors()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "architecture", "connectivity-model-vectors.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate connectivity-model-vectors.json");
    }
}
