namespace MoDi.App.Contracts.Connectivity;

public enum ChannelRuntimeState { Closed, Open }

public sealed record ChannelDescriptor(ChannelId Id, ChannelKind Kind, ChannelDirection Direction);

public static class AudioChannel
{
    public static readonly ChannelId Primary = ChannelId.Parse("audio/primary");
    public static readonly ChannelDescriptor Descriptor = new(Primary, ChannelKind.Audio, ChannelDirection.Duplex);
}

public interface IChannelDataPlane
{
    SessionId SessionId { get; }
    ChannelDescriptor Descriptor { get; }
    ChannelRuntimeState State { get; }
    long NextSequence { get; }
    event Action<ReadOnlyMemory<byte>>? BytesReceived;
    Task OpenAsync(CancellationToken cancellationToken);
    long ReserveSequence();
    void ResetSequence();
    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
}

public sealed class ChannelRouter
{
    private readonly object _gate = new();
    private readonly Dictionary<(SessionId Session, ChannelId Channel), IChannelDataPlane> _channels = [];

    public IReadOnlyList<IChannelDataPlane> Channels
    {
        get { lock (_gate) return _channels.Values.ToArray(); }
    }

    public void Register(IChannelDataPlane channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        lock (_gate)
        {
            if (!_channels.TryAdd((channel.SessionId, channel.Descriptor.Id), channel))
                throw new InvalidOperationException("Channel is already registered for this session.");
        }
    }

    public bool TryGet(SessionId sessionId, ChannelId channelId, out IChannelDataPlane? channel)
    {
        lock (_gate) return _channels.TryGetValue((sessionId, channelId), out channel);
    }

    public async Task CloseSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        IChannelDataPlane[] matches;
        lock (_gate)
        {
            matches = _channels.Where(pair => pair.Key.Session == sessionId).Select(pair => pair.Value).ToArray();
            foreach (var channel in matches) _channels.Remove((channel.SessionId, channel.Descriptor.Id));
        }
        foreach (var channel in matches) await channel.CloseAsync(cancellationToken).ConfigureAwait(false);
    }
}
