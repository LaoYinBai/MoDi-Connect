using System.Text.Json;
using MoDi.App.Contracts.Connectivity;

namespace MoDi.App.Contracts.Tests.Connectivity;

public sealed class ConnectivityModelTests
{
    private static readonly JsonDocument Vectors = JsonDocument.Parse(File.ReadAllText(FindVectors()));

    [Fact]
    public void Peer_identity_is_opaque_case_sensitive_and_never_a_display_name()
    {
        foreach (var vector in Vectors.RootElement.GetProperty("peerIdentities").EnumerateArray())
        {
            var left = PeerId.Parse(vector.GetProperty("left").GetString()!);
            var right = PeerId.Parse(vector.GetProperty("right").GetString()!);
            Assert.Equal(vector.GetProperty("equal").GetBoolean(), left == right);
            Assert.Equal(vector.GetProperty("left").GetString(), left.ToString());
        }
    }

    [Fact]
    public void Channel_id_validation_matches_the_shared_vectors()
    {
        foreach (var vector in Vectors.RootElement.GetProperty("channelIds").EnumerateArray())
        {
            var parsed = ChannelId.TryParse(vector.GetProperty("value").GetString()!, out _);
            Assert.Equal(vector.GetProperty("valid").GetBoolean(), parsed);
        }
    }

    [Fact]
    public void Session_state_transitions_match_the_shared_vectors()
    {
        foreach (var vector in Vectors.RootElement.GetProperty("stateTransitions").EnumerateArray())
        {
            var from = Enum.Parse<SessionState>(vector.GetProperty("from").GetString()!);
            var to = Enum.Parse<SessionState>(vector.GetProperty("to").GetString()!);
            Assert.Equal(vector.GetProperty("allowed").GetBoolean(), SessionStateMachine.CanTransition(from, to));
        }
    }

    [Fact]
    public void Current_connectivity_vocabulary_is_platform_neutral()
    {
        Assert.Equal(["Lan", "WifiDirect", "Bluetooth", "Usb"], Enum.GetNames<TransportKind>());
        Assert.Equal(["Audio", "Clipboard"], Enum.GetNames<ChannelKind>());
        Assert.Equal(["Send", "Receive", "Duplex"], Enum.GetNames<ChannelDirection>());
        Assert.Equal(["None", "Unavailable", "Unauthorized", "Timeout", "TransportFailure", "ProtocolFailure", "Cancelled"], Enum.GetNames<ConnectivityErrorCode>());
    }

    [Fact]
    public void Transport_descriptors_match_the_shared_vectors()
    {
        foreach (var vector in Vectors.RootElement.GetProperty("transportDescriptors").EnumerateArray())
        {
            var descriptor = TransportDescriptor.For(Enum.Parse<TransportKind>(vector.GetProperty("kind").GetString()!));
            Assert.Equal(vector.GetProperty("discover").GetBoolean(), descriptor.SupportsDiscovery);
            Assert.Equal(vector.GetProperty("listen").GetBoolean(), descriptor.SupportsListening);
            Assert.Equal(vector.GetProperty("connect").GetBoolean(), descriptor.SupportsConnecting);
            Assert.Equal(vector.GetProperty("optional").GetBoolean(), descriptor.IsOptional);
        }
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
