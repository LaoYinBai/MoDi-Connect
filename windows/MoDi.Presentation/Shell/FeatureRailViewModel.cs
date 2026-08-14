using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Shell;

public sealed class FeatureRailViewModel : ObservableObject, IDisposable
{
    public const double CompactWidth = 56d;
    public const double ExpandedWidth = 200d;
    private const double SnapMidpoint = (CompactWidth + ExpandedWidth) / 2d;

    private readonly IAppearanceService _appearance;
    private double _width;
    private bool _disposed;

    public FeatureRailViewModel(
        IAppearanceService appearance,
        IEnumerable<IBuiltInFeature> builtIns)
    {
        _appearance = appearance;
        _width = Clamp(appearance.Snapshot.FeatureRailWidth);
        Items = builtIns.Select(feature => new FeatureRailItemViewModel(feature)).ToArray();
        CommitWidthCommand = new AsyncRelayCommand(CommitWidthAsync);
        _appearance.SnapshotChanged += OnAppearanceChanged;
    }

    public IReadOnlyList<FeatureRailItemViewModel> Items { get; }
    public AsyncRelayCommand CommitWidthCommand { get; }

    public double Width
    {
        get => _width;
        private set
        {
            if (!SetProperty(ref _width, value))
                return;
            OnPropertyChanged(nameof(IsCompact));
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public bool IsCompact => Width == CompactWidth;
    public bool IsExpanded => !IsCompact;

    public void PreviewWidth(double requestedWidth) => Width = Clamp(requestedWidth);

    private async Task CommitWidthAsync(CancellationToken cancellationToken)
    {
        var snapped = Width < SnapMidpoint ? CompactWidth : ExpandedWidth;
        Width = snapped;
        await _appearance.SetFeatureRailWidthAsync(snapped, cancellationToken);
    }

    private void OnAppearanceChanged(AppearanceSnapshot snapshot) =>
        Width = Clamp(snapshot.FeatureRailWidth);

    private static double Clamp(double width) =>
        double.IsFinite(width) ? Math.Clamp(width, CompactWidth, ExpandedWidth) : ExpandedWidth;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _appearance.SnapshotChanged -= OnAppearanceChanged;
    }
}
