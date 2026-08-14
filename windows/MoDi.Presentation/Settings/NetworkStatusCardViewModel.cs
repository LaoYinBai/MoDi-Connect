using System.Collections.ObjectModel;
using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Settings;

public sealed class NetworkStatusCardViewModel : ObservableObject, IDisposable
{
    private readonly INetworkStatusSource _network;
    private readonly ObservableCollection<NetworkLinkRowViewModel> _links = [];
    private string _currentLinkLabel = string.Empty;
    private string _localIpAddress = string.Empty;
    private int _audioPort;
    private int _handshakePort;
    private bool _disposed;

    public NetworkStatusCardViewModel(INetworkStatusSource network)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
        Links = new ReadOnlyObservableCollection<NetworkLinkRowViewModel>(_links);
        ApplySnapshot(network.Snapshot);
        network.SnapshotChanged += OnSnapshotChanged;
    }

    public ReadOnlyObservableCollection<NetworkLinkRowViewModel> Links { get; }
    public string CurrentLinkLabel { get => _currentLinkLabel; private set => SetProperty(ref _currentLinkLabel, value); }
    public string LocalIpAddress { get => _localIpAddress; private set => SetProperty(ref _localIpAddress, value); }
    public int AudioPort { get => _audioPort; private set => SetProperty(ref _audioPort, value); }
    public int HandshakePort { get => _handshakePort; private set => SetProperty(ref _handshakePort, value); }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _network.SnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(NetworkStatusSnapshot snapshot) => ApplySnapshot(snapshot);

    private void ApplySnapshot(NetworkStatusSnapshot snapshot)
    {
        if (_disposed)
            return;

        CurrentLinkLabel = snapshot.CurrentLinkLabel;
        LocalIpAddress = snapshot.LocalIpAddress;
        AudioPort = snapshot.AudioPort;
        HandshakePort = snapshot.HandshakePort;
        _links.Clear();
        foreach (var link in snapshot.Links)
            _links.Add(new NetworkLinkRowViewModel(link.Kind, link.State, link.Label, link.Detail));
    }
}

public sealed record NetworkLinkRowViewModel(
    LinkKind Kind,
    LinkAvailability State,
    string Label,
    string Detail)
{
    public string StateLabel => State switch
    {
        LinkAvailability.Inactive => "未启动",
        LinkAvailability.Starting => "启动中",
        LinkAvailability.Listening => "监听中",
        LinkAvailability.Connecting => "连接中",
        LinkAvailability.Active => "已连接",
        LinkAvailability.Error => "异常",
        _ => State.ToString(),
    };
}
