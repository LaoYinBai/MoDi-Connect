using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace UITest.Fakes;

public sealed class FakeAppearanceService : IAppearanceService
{
    public AppearanceSnapshot Snapshot { get; private set; } = AppearanceSnapshot.Default;
    public int SelectPresetCalls { get; private set; }
    public event Action<AppearanceSnapshot>? SnapshotChanged;

    public Task<OperationResult> SelectPresetAsync(ThemePreset preset, CancellationToken cancellationToken)
    {
        SelectPresetCalls++;
        Publish(Snapshot with { Preset = preset });
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> SaveCustomPaletteAsync(CustomPalette palette, CancellationToken cancellationToken)
    {
        Publish(Snapshot with { Preset = ThemePreset.Custom, Palette = palette });
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> ImportBackgroundAsync(SelectedImage image, CancellationToken cancellationToken)
    {
        Publish(Snapshot with { BackgroundDisplayName = image.DisplayName });
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> SetReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken)
    {
        Publish(Snapshot with { ReduceMotion = reduceMotion });
        return Task.FromResult(OperationResult.Success());
    }

    public Task<OperationResult> SetFeatureRailWidthAsync(double width, CancellationToken cancellationToken)
    {
        Publish(Snapshot with { FeatureRailWidth = Math.Clamp(width, 56, 200) });
        return Task.FromResult(OperationResult.Success());
    }

    public void Reset() => Publish(AppearanceSnapshot.Default);

    public void Publish(AppearanceSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(snapshot);
    }
}
