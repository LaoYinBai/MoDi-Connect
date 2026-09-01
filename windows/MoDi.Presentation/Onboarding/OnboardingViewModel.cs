using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Onboarding;

public sealed class OnboardingViewModel : ObservableObject, IDisposable
{
    private static readonly string[] Titles = ["欢迎使用 MoDi", "准备手机与电脑", "连接诊断", "选择音频路线"];
    private static readonly string[] Descriptions =
    [
        "MoDi 让手机音频通过局域网、蓝牙或 USB 在 Windows 上播放或进入虚拟麦克风。",
        "请让手机与电脑处于同一网络；蓝牙需完成配对，USB 模式需开启 USB 调试并确认设备授权。",
        "检查 VB-CABLE、网络、防火墙端口、蓝牙与 USB 环境。单项失败不会阻止应用启动。",
        "扬声器路线直接播放；虚拟麦克风路线需要 VB-CABLE，并在目标软件中选择 CABLE Output。",
    ];
    private readonly IOnboardingService _service;
    private bool _isVisible;
    private int _currentStep;

    public OnboardingViewModel(IOnboardingService service)
    {
        _service = service;
        _currentStep = Math.Clamp(service.Snapshot.CurrentStep, 0, 3);
        NextCommand = new RelayCommand(Next);
        PreviousCommand = new RelayCommand(Previous);
        SkipCommand = new AsyncRelayCommand(SkipAsync);
        CompleteCommand = new AsyncRelayCommand(CompleteAsync);
        RunDiagnosticsCommand = new AsyncRelayCommand(RunDiagnosticsAsync);
        _service.SnapshotChanged += OnSnapshotChanged;
    }

    public RelayCommand NextCommand { get; }
    public RelayCommand PreviousCommand { get; }
    public AsyncRelayCommand SkipCommand { get; }
    public AsyncRelayCommand CompleteCommand { get; }
    public AsyncRelayCommand RunDiagnosticsCommand { get; }
    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }
    public int CurrentStep { get => _currentStep; private set { if (SetProperty(ref _currentStep, value)) { OnPropertyChanged(nameof(StepNumber)); OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Description)); OnPropertyChanged(nameof(IsFirstStep)); OnPropertyChanged(nameof(IsLastStep)); OnPropertyChanged(nameof(IsDiagnosticsStep)); } } }
    public int StepNumber => CurrentStep + 1;
    public string Title => Titles[CurrentStep];
    public string Description => Descriptions[CurrentStep];
    public bool IsFirstStep => CurrentStep == 0;
    public bool IsLastStep => CurrentStep == 3;
    public bool IsDiagnosticsStep => CurrentStep == 2;
    public IReadOnlyList<DiagnosticResult> Diagnostics => _service.Snapshot.Diagnostics;

    public void ShowIfIncomplete() => IsVisible = !_service.Snapshot.IsCompleted;
    private void Next() => CurrentStep = Math.Min(3, CurrentStep + 1);
    private void Previous() => CurrentStep = Math.Max(0, CurrentStep - 1);
    private async Task SkipAsync(CancellationToken token) { if ((await _service.SkipAsync(token)).IsSuccess) IsVisible = false; }
    private async Task CompleteAsync(CancellationToken token) { if ((await _service.CompleteAsync(token)).IsSuccess) IsVisible = false; }
    private async Task RunDiagnosticsAsync(CancellationToken token) { await _service.RunDiagnosticsAsync(token); OnPropertyChanged(nameof(Diagnostics)); }
    private void OnSnapshotChanged(OnboardingSnapshot snapshot) { OnPropertyChanged(nameof(Diagnostics)); if (snapshot.IsCompleted) IsVisible = false; }
    public void Dispose() => _service.SnapshotChanged -= OnSnapshotChanged;
}
