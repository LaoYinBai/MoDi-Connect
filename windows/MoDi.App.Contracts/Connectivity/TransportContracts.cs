namespace MoDi.App.Contracts.Connectivity;

public enum TransportSessionState
{
    Idle,
    Discovering,
    Listening,
    Connecting,
    Connected,
    Closing,
    Closed,
    Failed,
}

public sealed record TransportDescriptor(
    TransportKind Kind,
    bool SupportsDiscovery,
    bool SupportsListening,
    bool SupportsConnecting,
    bool IsOptional)
{
    public static TransportDescriptor For(TransportKind kind) => kind switch
    {
        TransportKind.Lan => new(kind, true, true, true, false),
        TransportKind.WifiDirect => new(kind, true, true, true, true),
        TransportKind.Bluetooth => new(kind, false, true, true, true),
        TransportKind.Usb => new(kind, false, true, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

public readonly record struct TransportEndpoint
{
    private TransportEndpoint(string value) => Value = value;
    public string Value { get; }

    public static TransportEndpoint Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Length > 512)
            throw new FormatException("Transport endpoint must be a non-empty opaque identifier.");
        return new TransportEndpoint(value);
    }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record TransportCandidate(
    TransportEndpoint Endpoint,
    string? DisplayName,
    PeerId? ConfirmedPeerId = null);

public interface ITransportSession : IAsyncDisposable
{
    TransportDescriptor Descriptor { get; }
    TransportSessionState State { get; }
    event Action<ReadOnlyMemory<byte>>? BytesReceived;
    Task<IReadOnlyList<TransportCandidate>> DiscoverAsync(CancellationToken cancellationToken);
    Task ListenAsync(CancellationToken cancellationToken);
    Task ConnectAsync(TransportEndpoint endpoint, CancellationToken cancellationToken);
    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
}

public sealed class TransportRegistry
{
    private readonly Dictionary<TransportKind, Func<ITransportSession>> _factories = [];

    public IReadOnlyCollection<TransportKind> RegisteredKinds => _factories.Keys;

    public void Register(TransportKind kind, Func<ITransportSession> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_factories.TryAdd(kind, factory))
            throw new InvalidOperationException($"Transport {kind} is already registered.");
    }

    public bool TryCreate(TransportKind kind, out ITransportSession? session)
    {
        if (!_factories.TryGetValue(kind, out var factory))
        {
            session = null;
            return false;
        }

        session = factory();
        return true;
    }
}
