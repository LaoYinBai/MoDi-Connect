using Avalonia.Media;
using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Shell;

public sealed class StatusBarViewModel : ObservableObject, IDisposable
{
    private readonly IReceiverStatusSource _receiver;
    private readonly IAudioSettingsService _audio;
    private ReceiverSnapshot _receiverSnapshot;
    private AudioSettingsSnapshot _audioSnapshot;
    private bool _disposed;

    public StatusBarViewModel(IReceiverStatusSource receiver, IAudioSettingsService audio)
    {
        _receiver = receiver;
        _audio = audio;
        _receiverSnapshot = receiver.Snapshot;
        _audioSnapshot = audio.Snapshot;
        _receiver.SnapshotChanged += OnReceiverChanged;
        _audio.SnapshotChanged += OnAudioChanged;
    }

    public string StateText => _receiverSnapshot.State switch
    {
        ReceiverState.Connected or ReceiverState.Streaming => "已连接",
        ReceiverState.Searching or ReceiverState.Found or ReceiverState.Connecting => "握手中",
        ReceiverState.Reconnecting => "重新连接中",
        ReceiverState.Error => "连接异常",
        _ => "未连接",
    };

    public IBrush StateBrush => new SolidColorBrush(Color.Parse(_receiverSnapshot.State switch
    {
        ReceiverState.Connected or ReceiverState.Streaming => "#47937F",
        ReceiverState.Searching or ReceiverState.Found or ReceiverState.Connecting or ReceiverState.Reconnecting => "#E8863C",
        _ => "#C2452D",
    }));

    public string StatusMessage =>
        $"当前链路 {LinkLabel(_receiverSnapshot.ActiveLink)} · 当前输出 {_audioSnapshot.OutputDeviceLabel}";
    public string VolumeText => $"音量 {Math.Round(_audioSnapshot.Volume * 100):0}%";
    public double VolumePercent => Math.Clamp(_audioSnapshot.Volume, 0, 1) * 100;

    private void OnReceiverChanged(ReceiverSnapshot snapshot)
    {
        _receiverSnapshot = snapshot;
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(StateBrush));
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void OnAudioChanged(AudioSettingsSnapshot snapshot)
    {
        _audioSnapshot = snapshot;
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(VolumeText));
        OnPropertyChanged(nameof(VolumePercent));
    }

    private static string LinkLabel(LinkKind link) => link switch
    {
        LinkKind.None => "无",
        LinkKind.WifiDirect => "WiFi Direct",
        LinkKind.Bluetooth => "蓝牙",
        LinkKind.Usb => "USB",
        _ => "LAN",
    };

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _receiver.SnapshotChanged -= OnReceiverChanged;
        _audio.SnapshotChanged -= OnAudioChanged;
    }
}
