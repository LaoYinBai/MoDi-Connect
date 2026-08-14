namespace MoDi.App.Contracts;

public interface IAppearanceService : IStateSource<AppearanceSnapshot>
{
    Task<OperationResult> SelectPresetAsync(ThemePreset preset, CancellationToken cancellationToken);
    Task<OperationResult> SaveCustomPaletteAsync(CustomPalette palette, CancellationToken cancellationToken);
    Task<OperationResult> ImportBackgroundAsync(SelectedImage image, CancellationToken cancellationToken);
    Task<OperationResult> SetReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken);
    Task<OperationResult> SetFeatureRailWidthAsync(double width, CancellationToken cancellationToken);
}
