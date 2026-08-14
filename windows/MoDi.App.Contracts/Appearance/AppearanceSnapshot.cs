namespace MoDi.App.Contracts;

public sealed record AppearanceSnapshot(
    ThemePreset Preset,
    CustomPalette Palette,
    string? BackgroundDisplayName,
    bool ReduceMotion,
    double FeatureRailWidth)
{
    public static AppearanceSnapshot Default { get; } = new(
        ThemePreset.InkNight,
        new CustomPalette(
            Background: "#151A1D",
            Surface: "#1B2226",
            SurfaceElevated: "#222B30",
            TextPrimary: "#F2EFE6",
            TextSecondary: "#A5AEA8",
            Accent: "#E8863C",
            Border: "#2E393F",
            Success: "#47937F"),
        BackgroundDisplayName: null,
        ReduceMotion: false,
        FeatureRailWidth: 200d);
}
