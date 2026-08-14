using System;
using System.Threading;
using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;
using UITest.Fakes;

namespace UITest.Demo;

public sealed class DemoControlsViewModel : ObservableObject, IDisposable
{
    private readonly FakeReceiverStatusSource _receiver;
    private readonly FakeAppearanceService _appearance;
    private readonly FakePairingService _pairing;
    private readonly FakePluginCatalogService _plugins;
    private readonly TimeProvider _timeProvider;
    private readonly SynchronizationContext? _uiContext;
    private readonly ITimer _pulseTimer;
    private bool _isOpen;
    private bool _autoRms;
    private double _rms = 0.72;
    private bool _disposed;

    public DemoControlsViewModel(
        FakeReceiverStatusSource receiver,
        FakeAppearanceService appearance,
        FakePairingService pairing,
        FakePluginCatalogService plugins,
        TimeProvider timeProvider)
    {
        _receiver = receiver;
        _appearance = appearance;
        _pairing = pairing;
        _plugins = plugins;
        _timeProvider = timeProvider;
        _uiContext = SynchronizationContext.Current;
        ToggleOpenCommand = new RelayCommand(() => IsOpen = !IsOpen);
        SetReceiverStateCommand = new RelayCommand<string>(SetReceiverState);
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
        RefreshQrCommand = new AsyncRelayCommand(_pairing.RefreshQrAsync);
        SetPluginScenarioCommand = new RelayCommand<string>(scenario => _plugins.SetScenario(scenario ?? "healthy"));
        _pulseTimer = timeProvider.CreateTimer(OnPulse, null, TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(80));
    }

    public RelayCommand ToggleOpenCommand { get; }
    public RelayCommand<string> SetReceiverStateCommand { get; }
    public AsyncRelayCommand ToggleThemeCommand { get; }
    public AsyncRelayCommand RefreshQrCommand { get; }
    public RelayCommand<string> SetPluginScenarioCommand { get; }

    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public bool AutoRms { get => _autoRms; set => SetProperty(ref _autoRms, value); }
    public double Rms
    {
        get => _rms;
        set
        {
            var normalized = Math.Clamp(value, 0, 1);
            if (SetProperty(ref _rms, normalized))
                _receiver.SetRms(normalized);
        }
    }
    public string RmsPercent => $"{Math.Round(Rms * 100):0}%";

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _pulseTimer.Dispose();
    }

    private void SetReceiverState(string? state)
    {
        _receiver.SetState(state?.ToLowerInvariant() switch
        {
            "handshaking" => ReceiverState.Connecting,
            "connected" => ReceiverState.Connected,
            "streaming" => ReceiverState.Streaming,
            "reconnecting" => ReceiverState.Reconnecting,
            "error" => ReceiverState.Error,
            _ => ReceiverState.Idle,
        }, Rms);
    }

    private async System.Threading.Tasks.Task ToggleThemeAsync(CancellationToken cancellationToken)
    {
        var target = _appearance.Snapshot.Preset == ThemePreset.PaperDay
            ? ThemePreset.InkNight
            : ThemePreset.PaperDay;
        await _appearance.SelectPresetAsync(target, cancellationToken);
    }

    private void OnPulse(object? state)
    {
        if (_disposed || !AutoRms)
            return;

        if (_uiContext is not null)
        {
            _uiContext.Post(static target => ((DemoControlsViewModel)target!).ApplyPulse(), this);
            return;
        }

        ApplyPulse();
    }

    private void ApplyPulse()
    {
        if (_disposed || !AutoRms)
            return;

        var seconds = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds() / 1000d;
        Rms = 0.58 + (0.28 * ((Math.Sin(seconds * 5) + 1) / 2));
        OnPropertyChanged(nameof(RmsPercent));
    }
}
