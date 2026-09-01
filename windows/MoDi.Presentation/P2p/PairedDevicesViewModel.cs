using Avalonia;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.P2p;

public sealed class PairedDevicesViewModel : ObservableObject, IDisposable
{
    private readonly IPairingService _pairing;
    private readonly ObservableCollection<PairedDeviceItemViewModel> _devices = [];
    private string? _selectedDeviceId;
    private bool _isConnecting;
    private bool _isOpen;
    private string? _errorCode;
    private string? _errorMessage;
    private bool _disposed;

    public PairedDevicesViewModel(IPairingService pairing)
    {
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        Devices = new ReadOnlyObservableCollection<PairedDeviceItemViewModel>(_devices);
        ConnectCommand = new AsyncRelayCommand<string>(ConnectAsync, CanConnect);
        PairNewDeviceCommand = new RelayCommand(() => PairNewDeviceRequested?.Invoke(this, EventArgs.Empty));
        ApplySnapshot(pairing.Snapshot);
        pairing.SnapshotChanged += OnSnapshotChanged;
    }

    public event EventHandler? PairNewDeviceRequested;

    public ReadOnlyObservableCollection<PairedDeviceItemViewModel> Devices { get; }
    public AsyncRelayCommand<string> ConnectCommand { get; }
    public RelayCommand PairNewDeviceCommand { get; }
    public bool IsEmpty => Devices.Count == 0;
    public bool HasDevices => !IsEmpty;

    public string? SelectedDeviceId
    {
        get => _selectedDeviceId;
        private set => SetProperty(ref _selectedDeviceId, value);
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        private set => SetProperty(ref _isConnecting, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value))
                return;

            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;
    public void Toggle() => IsOpen = !IsOpen;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pairing.SnapshotChanged -= OnSnapshotChanged;
    }

    private bool CanConnect(string? deviceId) =>
        !_disposed && !string.IsNullOrWhiteSpace(deviceId) && _devices.Any(device => device.Id == deviceId);

    private async Task ConnectAsync(string? deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        Select(deviceId);
        IsConnecting = true;
        ErrorCode = null;
        ErrorMessage = null;
        try
        {
            var result = await _pairing.ConnectAsync(deviceId, cancellationToken);
            if (!result.IsSuccess)
            {
                ErrorCode = result.ErrorCode;
                ErrorMessage = result.UserMessage;
            }
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void OnSnapshotChanged(PairingSnapshot snapshot) => RunOnCapturedContext(() => ApplySnapshot(snapshot));

    private void ApplySnapshot(PairingSnapshot snapshot)
    {
        if (_disposed)
            return;

        _devices.Clear();
        foreach (var device in snapshot.Devices)
        {
            _devices.Add(new PairedDeviceItemViewModel(
                device.Id,
                device.DisplayName,
                device.LastConnectedLabel,
                device.Id == SelectedDeviceId));
        }

        if (SelectedDeviceId is not null && _devices.All(device => device.Id != SelectedDeviceId))
            SelectedDeviceId = null;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasDevices));
        ConnectCommand.RaiseCanExecuteChanged();
    }

    private void Select(string deviceId)
    {
        SelectedDeviceId = deviceId;
        foreach (var device in _devices)
            device.SetSelected(device.Id == deviceId);
    }

    private static void RunOnCapturedContext(Action action)
    {
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}

public sealed class PairedDeviceItemViewModel : ObservableObject
{
    private bool _isSelected;

    internal PairedDeviceItemViewModel(
        string id,
        string displayName,
        string lastConnectedLabel,
        bool isSelected)
    {
        Id = id;
        DisplayName = displayName;
        LastConnectedLabel = lastConnectedLabel;
        _isSelected = isSelected;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string LastConnectedLabel { get; }
    public string LinkLabel => "P2P";
    public bool IsSelected
    {
        get => _isSelected;
        private set => SetProperty(ref _isSelected, value);
    }

    internal void SetSelected(bool isSelected) => IsSelected = isSelected;
}
