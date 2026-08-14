using MoDi.App.Contracts;

namespace MoDi.Presentation.Tests.TestDoubles;

internal sealed class RecordingAppearanceService : IAppearanceService
{
    public RecordingAppearanceService(AppearanceSnapshot? snapshot = null) =>
        Snapshot = snapshot ?? AppearanceSnapshot.Default;

    public AppearanceSnapshot Snapshot { get; private set; }
    public OperationResult SelectPresetResult { get; set; } = OperationResult.Success();
    public OperationResult SavePaletteResult { get; set; } = OperationResult.Success();
    public OperationResult ImportBackgroundResult { get; set; } = OperationResult.Success();
    public OperationResult SetReduceMotionResult { get; set; } = OperationResult.Success();
    public OperationResult SetFeatureRailWidthResult { get; set; } = OperationResult.Success();
    public int SelectPresetCalls { get; private set; }
    public int SavePaletteCalls { get; private set; }
    public int ImportBackgroundCalls { get; private set; }
    public int SetReduceMotionCalls { get; private set; }
    public int SetFeatureRailWidthCalls { get; private set; }
    public ThemePreset? LastPreset { get; private set; }
    public CustomPalette? LastPalette { get; private set; }
    public SelectedImage? LastImportedImage { get; private set; }
    public bool? LastReduceMotion { get; private set; }
    public double? LastFeatureRailWidth { get; private set; }
    public event Action<AppearanceSnapshot>? SnapshotChanged;

    public Task<OperationResult> SelectPresetAsync(ThemePreset preset, CancellationToken cancellationToken)
    {
        SelectPresetCalls++;
        LastPreset = preset;
        if (SelectPresetResult.IsSuccess)
            Publish(Snapshot with { Preset = preset });
        return Task.FromResult(SelectPresetResult);
    }

    public Task<OperationResult> SaveCustomPaletteAsync(CustomPalette palette, CancellationToken cancellationToken)
    {
        SavePaletteCalls++;
        LastPalette = palette;
        if (SavePaletteResult.IsSuccess)
            Publish(Snapshot with { Preset = ThemePreset.Custom, Palette = palette });
        return Task.FromResult(SavePaletteResult);
    }

    public Task<OperationResult> ImportBackgroundAsync(SelectedImage image, CancellationToken cancellationToken)
    {
        ImportBackgroundCalls++;
        LastImportedImage = image;
        if (ImportBackgroundResult.IsSuccess)
            Publish(Snapshot with { BackgroundDisplayName = image.DisplayName });
        return Task.FromResult(ImportBackgroundResult);
    }

    public Task<OperationResult> SetReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken)
    {
        SetReduceMotionCalls++;
        LastReduceMotion = reduceMotion;
        if (SetReduceMotionResult.IsSuccess)
            Publish(Snapshot with { ReduceMotion = reduceMotion });
        return Task.FromResult(SetReduceMotionResult);
    }

    public Task<OperationResult> SetFeatureRailWidthAsync(double width, CancellationToken cancellationToken)
    {
        SetFeatureRailWidthCalls++;
        LastFeatureRailWidth = width;
        if (SetFeatureRailWidthResult.IsSuccess)
            Publish(Snapshot with { FeatureRailWidth = width });
        return Task.FromResult(SetFeatureRailWidthResult);
    }

    public void Publish(AppearanceSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }
}
